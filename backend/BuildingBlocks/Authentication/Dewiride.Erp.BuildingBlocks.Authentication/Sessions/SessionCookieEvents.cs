using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.Extensibility;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Sessions;

// Every caller of the API is a script or the web app's server, never a page that could follow a redirect to a sign-in
// form, so a refused request answers a problem without a Location header. Sliding renewal re-issues the cookie with the
// same properties, so the sign-in time stamped here bounds the whole session however often it slides. The handler alone
// renews only after half the idle timeout, so a person idle for just over half of it could be signed out; renewing on the
// first request more than a minute, or half the idle timeout when that is shorter, after the cookie was issued keeps every
// session alive for the idle timeout less at most that interval, and lets a session of the shortest idle timeout slide.
// A session is accepted only when it was issued after the person last signed out, which SessionRevocations records, and while
// the person's account is in the token cache, the one entry per person in SQL Server that signing out removes; the record is
// read first, so a refused copy of a cookie costs one read and never slides the person's token cache entry:
// signing out ends every session of that person, on every device and browser and on every instance of the API, at once, and
// a copy of one of those cookies stays refused after the person signs in again, while a restart of the API ends none. A
// token cache or sign-out record that cannot be read fails the request instead, and leaves the cookie as it is, so a passing
// database failure signs nobody out.
internal sealed class SessionCookieEvents(
    TimeProvider timeProvider,
    IOptions<EntraSignInOptions> signIn,
    IProblemDetailsService problemDetails,
    IConfidentialClientApplicationProvider applications,
    SessionRevocations revocations) : CookieAuthenticationEvents
{
    public const string SignedInAtItem = "erp.signed-in-at";

    public const string UnauthenticatedTitle = "Sign in to use this API.";

    public const string ForbiddenTitle = "The signed-in person may not do this.";

    private static readonly TimeSpan MaxRenewalInterval = TimeSpan.FromMinutes(1);

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized, ProblemTypes.RequestUnauthenticated, UnauthenticatedTitle);
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden, ProblemTypes.RequestForbidden, ForbiddenTitle);
    }

    public override Task SigningIn(CookieSigningInContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Properties.IsPersistent = false;
        context.Properties.Items[SignedInAtItem] = timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);

        return Task.CompletedTask;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (SignedInAt(context.Properties) is { } signedInAt
            && timeProvider.GetUtcNow() - signedInAt < signIn.Value.SessionLifetime
            && context.Principal?.GetMsalAccountId() is { } accountId
            && !await revocations.IsRevokedAsync(accountId, signedInAt, context.HttpContext.RequestAborted).ConfigureAwait(false)
            && await IsAccountCachedAsync(accountId).ConfigureAwait(false))
        {
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
    }

    public override Task CheckSlidingExpiration(CookieSlidingExpirationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var halfIdleTimeout = signIn.Value.SessionIdleTimeout / 2;
        context.ShouldRenew = context.ElapsedTime > (halfIdleTimeout < MaxRenewalInterval ? halfIdleTimeout : MaxRenewalInterval);

        return Task.CompletedTask;
    }

    private static DateTimeOffset? SignedInAt(AuthenticationProperties properties) =>
        properties.Items.TryGetValue(SignedInAtItem, out var stamp)
        && DateTimeOffset.TryParseExact(stamp, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var signedInAt)
            ? signedInAt
            : null;

    private async Task<bool> IsAccountCachedAsync(string accountId)
    {
        var application = await applications.GetConfidentialClientApplicationAsync(OpenIdConnectDefaults.AuthenticationScheme).ConfigureAwait(false);

        return await application.GetAccountAsync(accountId).ConfigureAwait(false) is not null;
    }

    private async Task WriteProblemAsync(HttpContext httpContext, int status, string code, string title)
    {
        httpContext.Response.StatusCode = status;
        await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Extensions = { [ResultExtensions.CodeExtension] = code },
            },
        }).ConfigureAwait(false);
    }
}
