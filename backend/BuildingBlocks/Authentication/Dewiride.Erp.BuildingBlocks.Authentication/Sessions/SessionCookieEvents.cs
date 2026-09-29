using System.Globalization;
using System.Security.Claims;
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
// first request after a minute keeps every session alive for the idle timeout less at most a minute. Signing out removes
// the person's account from the token cache, and a restart of the API empties the in-memory cache, so a session whose
// account is missing from the cache is refused and cleared: every copy of a signed-out cookie stops working at once.
internal sealed class SessionCookieEvents(
    TimeProvider timeProvider,
    IOptions<EntraSignInOptions> signIn,
    IProblemDetailsService problemDetails,
    IConfidentialClientApplicationProvider applications) : CookieAuthenticationEvents
{
    public const string SignedInAtItem = "erp.signed-in-at";

    public const string UnauthenticatedTitle = "Sign in to use this API.";

    public const string ForbiddenTitle = "The signed-in person may not do this.";

    private static readonly TimeSpan RenewalInterval = TimeSpan.FromMinutes(1);

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

        if (IsWithinLifetime(context.Properties) && await IsAccountCachedAsync(context.Principal).ConfigureAwait(false))
        {
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
    }

    public override Task CheckSlidingExpiration(CookieSlidingExpirationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.ShouldRenew = context.ElapsedTime > RenewalInterval;

        return Task.CompletedTask;
    }

    private bool IsWithinLifetime(AuthenticationProperties properties) =>
        properties.Items.TryGetValue(SignedInAtItem, out var stamp)
        && DateTimeOffset.TryParseExact(stamp, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var signedInAt)
        && timeProvider.GetUtcNow() - signedInAt < signIn.Value.SessionLifetime;

    private async Task<bool> IsAccountCachedAsync(ClaimsPrincipal? principal)
    {
        if (principal?.GetMsalAccountId() is not { } accountId)
        {
            return false;
        }

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
