using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;
using Dewiride.Erp.BuildingBlocks.Authentication.Endpoints.Requests;
using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Web;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Endpoints;

internal static class AuthEndpoints
{
    public const string Tag = "Auth";

    public const string LoginRouteName = "Auth.Login";

    public const string LogoutRouteName = "Auth.Logout";

    public const string AntiforgeryRouteName = "Auth.Antiforgery";

    public const string FetchModeHeader = "Sec-Fetch-Mode";

    public const string FetchDestinationHeader = "Sec-Fetch-Dest";

    public const string FetchSiteHeader = "Sec-Fetch-Site";

    public const string NotAPageMessage = "The sign-in starts only from a page the browser opens itself, never from an image, a frame or a script.";

    public const string NotThisSiteMessage = "The antiforgery tokens are issued again only to a script of this site, never to a page, an image or a frame another site opens.";

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

        // A cross-site form post carries no SameSite=Lax session cookie, so the fallback policy refuses it before anything is
        // signed out. The page signs out with a form it posts, which can carry the antiforgery token only in a form field.
        group.MapPost("/logout", LogoutAsync)
            .WithName(LogoutRouteName)
            .WithSummary("Ends the session and redirects to the Microsoft Entra end-session endpoint, which returns to the sign-in page.")
            .WithMetadata(new RequireAntiforgeryTokenAttribute())
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/antiforgery", IssueAntiforgeryTokens)
            .WithName(AntiforgeryRouteName)
            .WithSummary("Issues the signed-in person's antiforgery tokens again: the request token in the readable cookie __Host-erp-xsrf, sent back in the X-XSRF-TOKEN header of every POST, PUT, PATCH and DELETE.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
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

    private static async Task<SignOutHttpResult> LogoutAsync(ClaimsPrincipal user, SessionRevocations revocations)
    {
        // The sign-out is recorded before anything is signed out: a record that cannot be written fails the request and leaves
        // the person signed in, and a written one refuses every older session of the person even if removing their account fails.
        // It is written whatever happens to the request meanwhile, so a sign-out the browser abandons still takes effect; the
        // retry limits and the command timeout bound it.
        if (user.GetMsalAccountId() is { } accountId)
        {
            await revocations.RevokeAsync(accountId, CancellationToken.None).ConfigureAwait(false);
        }

        return TypedResults.SignOut(
            new AuthenticationProperties { RedirectUri = AuthPaths.LoginPage },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
    }

    // A page, image or frame of another site that sends the browser here would replace the person's tokens and break a token a
    // page of this site already holds; the web app's own calls are same-origin, and its server sends no Sec-Fetch-Site.
    private static Results<NoContent, ProblemHttpResult> IssueAntiforgeryTokens(HttpContext httpContext, AntiforgeryCookies cookies)
    {
        if (!IsAbsentOrExactly(httpContext.Request.Headers[FetchSiteHeader], "same-origin"))
        {
            return Error.Validation(ProblemTypes.RequestInvalid, NotThisSiteMessage).ToProblem();
        }

        cookies.Issue(httpContext, httpContext.User);

        return TypedResults.NoContent();
    }

    private static bool IsOpenedAsAPage(IHeaderDictionary headers) =>
        IsAbsentOrExactly(headers[FetchModeHeader], "navigate") && IsAbsentOrExactly(headers[FetchDestinationHeader], "document");

    private static bool IsAbsentOrExactly(StringValues values, string expected) =>
        values.Count == 0 || (values.Count == 1 && string.Equals(values[0], expected, StringComparison.Ordinal));
}
