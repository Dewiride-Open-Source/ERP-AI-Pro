using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;
using Dewiride.Erp.BuildingBlocks.Authentication.Endpoints.Requests;
using Dewiride.Erp.BuildingBlocks.Authentication.Endpoints.Responses;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.SecurityEvents;
using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Endpoints;

internal static class AuthEndpoints
{
    public const string Tag = "Auth";

    public const string LoginRouteName = "Auth.Login";

    public const string LogoutRouteName = "Auth.Logout";

    public const string AntiforgeryRouteName = "Auth.Antiforgery";

    public const string MeRouteName = "Auth.Me";

    public const string SessionRouteName = "Auth.Session";

    public const string RenewSessionRouteName = "Auth.RenewSession";

    public const string FrontChannelSignOutRouteName = "Auth.FrontChannelSignOut";

    public const string FetchModeHeader = "Sec-Fetch-Mode";

    public const string FetchDestinationHeader = "Sec-Fetch-Dest";

    public const string FetchSiteHeader = "Sec-Fetch-Site";

    public const string NotAPageMessage = "The sign-in starts only from a page the browser opens itself, never from an image, a frame or a script.";

    public const string NotThisSiteMessage = "The antiforgery tokens are issued again only to a script of this site, never to a page, an image or a frame another site opens.";

    // Only the session cookie has a session: a request another scheme signs in has no expiry to report or extend, so it is
    // refused like an anonymous one.
    private static readonly AuthorizationPolicy SessionCookieOnly = new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(AuthPaths.Prefix)
            .WithTags(Tag)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        group.MapGet("/login", Login)
            .WithName(LoginRouteName)
            .WithSummary("Starts the Microsoft Entra sign-in and returns to the local path afterwards; a signed-in caller goes straight to that path.")
            .Produces(StatusCodes.Status302Found)
            .ProducesValidationProblem()
            .AllowAnonymous();

        // The page signs out with a form it posts, which can carry the antiforgery token only in a form field. A request
        // without a session, from a page whose session already ended, still goes on to Entra's end-session endpoint, so the
        // Microsoft sign-in of a shared browser ends too; it changes no cookie and does no more than a link to that public
        // address does. The fetch metadata a browser sends confines that to a page of this site: another site's form post,
        // which carries no SameSite=Lax session cookie, is refused as any anonymous request is, so it sends no browser to
        // Entra either.
        group.MapPost("/logout", LogoutAsync)
            .WithName(LogoutRouteName)
            .WithSummary("Ends the session, when the request carries one, and redirects to the Microsoft Entra end-session endpoint, which returns to the sign-in page.")
            .WithMetadata(new RequireAntiforgeryTokenAttribute())
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .AllowAnonymous();

        group.MapGet("/antiforgery", IssueAntiforgeryTokens)
            .WithName(AntiforgeryRouteName)
            .WithSummary("Issues the signed-in person's antiforgery tokens again: the request token in the readable cookie __Host-erp-xsrf, sent back in the X-XSRF-TOKEN header of every POST, PUT, PATCH and DELETE.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", GetCurrentUser)
            .WithName(MeRouteName)
            .WithSummary("Returns the signed-in person: their Microsoft Entra ID object id, display name, sign-in name and app roles.")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/session", ReadSessionAsync)
            .WithName(SessionRouteName)
            .WithSummary("Returns when the session ends unless a request renews it and when it ends at the latest, without renewing it.")
            .RequireAuthorization(SessionCookieOnly)
            .WithMetadata(new SessionRenewalMetadata(Renews: false))
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/session", RenewSessionAsync)
            .WithName(RenewSessionRouteName)
            .WithSummary("Renews the session at once, for the idle timeout from now within the session lifetime, and returns when it ends.")
            .RequireAuthorization(SessionCookieOnly)
            .WithMetadata(new SessionRenewalMetadata(Renews: true))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Entra loads this from a hidden frame of its own site, which carries no session cookie, so the route is anonymous and
        // ends the sessions of the Entra session it names (FrontChannelSignOut). It is a protocol callback like those the
        // OpenID Connect handler answers, so the API client has no method for it, and it answers every well-formed request
        // alike, so the answer never says whether a session ended.
        group.MapGet("/signout-oidc", SignOutFrontChannelAsync)
            .WithName(FrontChannelSignOutRouteName)
            .WithSummary("Ends the sessions signed in from the Microsoft Entra ID session the front-channel sign-out names.")
            .ExcludeFromDescription()
            .AllowAnonymous();
    }

    private static Results<RedirectHttpResult, ChallengeHttpResult, ProblemHttpResult> Login([AsParameters] LoginRequest login, HttpContext httpContext)
    {
        // Every challenge writes a correlation and a nonce cookie, so another site that made the browser request this path
        // from images or frames again and again could fill the person's cookies until this site refused their requests. A
        // browser marks a page it opens itself as navigate and document; a client that sends neither header is no browser.
        if (!IsOpenedAsAPage(httpContext.Request.Headers))
        {
            return Error.Validation(ProblemTypes.RequestInvalid, NotAPageMessage).ToProblem();
        }

        return httpContext.User.Identity?.IsAuthenticated == true
            ? TypedResults.LocalRedirect(login.LocalReturnUrl)
            : TypedResults.Challenge(new AuthenticationProperties { RedirectUri = login.LocalReturnUrl }, [OpenIdConnectDefaults.AuthenticationScheme]);
    }

    // A browser without a session reaches only Entra's end-session endpoint, which then asks which account to sign out. When
    // the request carries a session, the sign-out is recorded before anything is signed out: a record that cannot be written
    // fails the request and leaves the person signed in, and a written one refuses every older session of the person even if
    // removing their account fails. The record of the Entra session the person signed in from is removed next, so nothing of
    // that Entra session outlives the sign-out. Both run whatever happens to the request meanwhile, so a sign-out the browser
    // abandons still takes effect; the retry limits and the command timeout bound them. The security event of the sign-out is
    // recorded last, and one that cannot be written is logged without keeping the person signed in.
    private static async Task<Results<SignOutHttpResult, ChallengeHttpResult>> LogoutAsync(HttpContext httpContext, SessionRevocations revocations, EntraSessions entraSessions)
    {
        var user = httpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return IsAbsentOrExactly(httpContext.Request.Headers[FetchSiteHeader], "same-origin")
                ? TypedResults.SignOut(SignedOut(), [OpenIdConnectDefaults.AuthenticationScheme])
                : TypedResults.Challenge(authenticationSchemes: [CookieAuthenticationDefaults.AuthenticationScheme]);
        }

        if (user.GetMsalAccountId() is { } accountId)
        {
            await revocations.RevokeAsync(accountId, CancellationToken.None).ConfigureAwait(false);
        }

        if (user.FindFirst(JwtRegisteredClaimNames.Sid)?.Value is { Length: > 0 } entraSessionId)
        {
            await entraSessions.ForgetAsync(entraSessionId, CancellationToken.None).ConfigureAwait(false);
        }

        await SignInAudit.TryRecordAsync(httpContext, SecurityEventKind.SignedOut, actor: SignInAudit.ObjectIdOf(user)).ConfigureAwait(false);

        return TypedResults.SignOut(SignedOut(), [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
    }

    private static async Task<Ok> SignOutFrontChannelAsync([AsParameters] FrontChannelSignOutRequest request, FrontChannelSignOut signOut, HttpContext httpContext)
    {
        await signOut.SignOutAsync(httpContext, request.Iss!, request.Sid!).ConfigureAwait(false);

        return TypedResults.Ok();
    }

    // A navigation another site starts carries the SameSite=Lax session cookie but not the SameSite=Strict cookie token, so
    // issuing the pair for it would write a new cookie token and refuse every request token a page of this site holds. The
    // web app's scripts send same-origin and its server sends no Sec-Fetch-Site, so every other value is refused.
    private static Results<NoContent, ProblemHttpResult> IssueAntiforgeryTokens(HttpContext httpContext, AntiforgeryCookies cookies)
    {
        if (!IsAbsentOrExactly(httpContext.Request.Headers[FetchSiteHeader], "same-origin"))
        {
            return Error.Validation(ProblemTypes.RequestInvalid, NotThisSiteMessage).ToProblem();
        }

        cookies.Issue(httpContext, httpContext.User);

        return TypedResults.NoContent();
    }

    private static Ok<CurrentUserResponse> GetCurrentUser(ClaimsPrincipal user) =>
        TypedResults.Ok(new CurrentUserResponse(
            Guid.TryParse(RequiredClaim(user, ClaimConstants.Oid), out var objectId) ? objectId : throw MissingClaim(ClaimConstants.Oid),
            RequiredClaim(user, ClaimConstants.Name),
            RequiredClaim(user, ClaimConstants.PreferredUserName),
            [.. user.FindAll(ClaimConstants.Roles).Select(role => role.Value)]));

    private static async Task<Ok<SessionResponse>> ReadSessionAsync(HttpContext httpContext, IOptions<EntraSignInOptions> signIn)
    {
        var properties = await SessionPropertiesAsync(httpContext).ConfigureAwait(false);

        return TypedResults.Ok(SessionTimes(properties.ExpiresUtc!.Value, properties, signIn.Value));
    }

    // The expiry adds the span the cookie was issued with to the clock reading SessionCookieEvents records; the handler renews
    // from a reading it takes after that one, and AuthenticationProperties keeps an expiry to the second, so this is the
    // renewed cookie's expiry or a second earlier.
    private static async Task<Ok<SessionResponse>> RenewSessionAsync(HttpContext httpContext, IOptions<EntraSignInOptions> signIn)
    {
        var properties = await SessionPropertiesAsync(httpContext).ConfigureAwait(false);
        var expiresAt = SessionCookieEvents.RenewedAt(httpContext) is { } renewedAt
            ? ToTheSecond(renewedAt + (properties.ExpiresUtc!.Value - properties.IssuedUtc!.Value))
            : properties.ExpiresUtc!.Value;

        return TypedResults.Ok(SessionTimes(expiresAt, properties, signIn.Value));
    }

    private static async Task<AuthenticationProperties> SessionPropertiesAsync(HttpContext httpContext) =>
        (await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false)).Properties!;

    // Both times are to the second and never later than the session really ends.
    private static SessionResponse SessionTimes(DateTimeOffset idleExpiry, AuthenticationProperties properties, EntraSignInOptions signIn)
    {
        var lifetimeEndsAt = ToTheSecond(SessionCookieEvents.SignedInAt(properties)!.Value + signIn.SessionLifetime);

        return new SessionResponse(idleExpiry < lifetimeEndsAt ? idleExpiry : lifetimeEndsAt, lifetimeEndsAt);
    }

    private static DateTimeOffset ToTheSecond(DateTimeOffset time) => time.AddTicks(-(time.Ticks % TimeSpan.TicksPerSecond));

    private static AuthenticationProperties SignedOut() => new() { RedirectUri = AuthPaths.SignedOutPage };

    private static string RequiredClaim(ClaimsPrincipal user, string type) => user.FindFirstValue(type) ?? throw MissingClaim(type);

    private static InvalidOperationException MissingClaim(string type) =>
        new($"The signed-in principal carries no valid {type} claim; every ERP sign-in is a Microsoft Entra ID account with an object id, a display name and a sign-in name.");

    private static bool IsOpenedAsAPage(IHeaderDictionary headers) =>
        IsAbsentOrExactly(headers[FetchModeHeader], "navigate") && IsAbsentOrExactly(headers[FetchDestinationHeader], "document");

    private static bool IsAbsentOrExactly(StringValues values, string expected) =>
        values.Count == 0 || (values.Count == 1 && string.Equals(values[0], expected, StringComparison.Ordinal));
}
