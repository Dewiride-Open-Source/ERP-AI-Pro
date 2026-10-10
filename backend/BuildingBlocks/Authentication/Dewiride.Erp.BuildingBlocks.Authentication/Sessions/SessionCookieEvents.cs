using System.Globalization;
using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.SecurityEvents;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.Extensibility;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Sessions;

// Sliding renewal re-issues the cookie with the same properties, so the sign-in time stamped here bounds the whole session
// however often it slides. The handler alone renews only after half the idle timeout, so a person idle for just over half
// of it could be signed out; renewing on the first request more than a minute, or half the idle timeout when that is
// shorter, after the cookie was issued keeps every session alive for the idle timeout less at most that interval, and lets
// a session of the shortest idle timeout slide. The session endpoints decide renewal themselves (SessionRenewalMetadata).
// A session starts only once the person's record admits them (IPersonAdmission), which creates or refreshes the record from
// the sign-in, and it is accepted only when it was issued after the person last signed out, which SessionRevocations
// records, while the Entra session it was signed in from is recorded for the person (EntraSessions), while the person's
// record still admits them, so a deactivated or deleted person's sessions end at their next request, and while the person's
// account is in the token cache, the one entry per person in SQL Server that signing out removes; the token cache is read
// last, so a refused copy of a cookie never slides the person's token cache entry. Signing out ends every session of that
// person, on every device and browser and on every instance of the API, at once, and a copy of one of those cookies stays
// refused after the person signs in again; Entra's front-channel sign-out ends only the sessions signed in from the Entra
// session it names; a restart of the API ends none. Entra always sends an Entra session id, and a principal without one
// cannot be named by that sign-out, so only the other checks apply to it. A token cache entry or record, the person's
// included, that cannot be read fails the request instead, and leaves the cookie as it is, so a passing database failure
// signs nobody out. Every sign-in
// records the Entra session it came from, and a record that cannot be written fails the sign-in before the cookie is
// issued, so a store failure never leaves a session the front-channel sign-out cannot end. Every sign-in issues the
// person's antiforgery tokens, and every sign-out, a refused cookie's included, clears them, so a browser holds tokens only
// for the person signed in on it.
internal sealed class SessionCookieEvents(
    TimeProvider timeProvider,
    IOptions<EntraSignInOptions> signIn,
    IProblemDetailsService problemDetails,
    IConfidentialClientApplicationProvider applications,
    SessionRevocations revocations,
    EntraSessions entraSessions,
    AntiforgeryCookies antiforgeryCookies,
    IPersonAdmission admission) : CookieAuthenticationEvents
{
    public const string SignedInAtItem = "erp.signed-in-at";

    private static readonly TimeSpan MaxRenewalInterval = TimeSpan.FromMinutes(1);

    private static readonly object RenewedAtItem = new();

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return AuthenticationProblems.WriteUnauthenticatedAsync(context.HttpContext, problemDetails);
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return AuthenticationProblems.WriteForbiddenAsync(context.HttpContext, problemDetails);
    }

    public override async Task SigningIn(CookieSigningInContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var principal = context.Principal!;

        // The audit columns of a record are stamped from the request's actor, so the record a first sign-in creates is the
        // person's own.
        context.HttpContext.User = principal;
        if (!await admission.AdmitAsync(PersonOf(principal), context.HttpContext.RequestAborted).ConfigureAwait(false))
        {
            throw new SignInRefusedException();
        }

        context.Properties.IsPersistent = false;
        context.Properties.Items[SignedInAtItem] = timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);

        await entraSessions.RecordAsync(principal, context.HttpContext.RequestAborted).ConfigureAwait(false);
    }

    public override Task SignedIn(CookieSignedInContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        antiforgeryCookies.Issue(context.HttpContext, context.Principal!);

        return Task.CompletedTask;
    }

    public override Task SigningOut(CookieSigningOutContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        AntiforgeryCookies.Clear(context.HttpContext);

        return Task.CompletedTask;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (SignedInAt(context.Properties) is { } signedInAt
            && timeProvider.GetUtcNow() - signedInAt < signIn.Value.SessionLifetime
            && context.Principal?.GetMsalAccountId() is { } accountId
            && !await revocations.IsRevokedAsync(accountId, signedInAt, context.HttpContext.RequestAborted).ConfigureAwait(false)
            && await IsInItsEntraSessionAsync(context.Principal, accountId, context.HttpContext.RequestAborted).ConfigureAwait(false)
            && await IsAdmittedAsync(context.Principal, context.HttpContext.RequestAborted).ConfigureAwait(false)
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

        // The handler renews from a clock reading it takes after this event, never before the one it took for it, which is
        // IssuedUtc plus ElapsedTime, so an expiry computed from that reading is never later than the renewed cookie's.
        if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<SessionRenewalMetadata>() is { } renewal)
        {
            context.ShouldRenew = renewal.Renews;
            if (renewal.Renews)
            {
                context.HttpContext.Items[RenewedAtItem] = context.Properties.IssuedUtc!.Value + context.ElapsedTime;
            }

            return Task.CompletedTask;
        }

        var halfIdleTimeout = signIn.Value.SessionIdleTimeout / 2;
        context.ShouldRenew = context.ElapsedTime > (halfIdleTimeout < MaxRenewalInterval ? halfIdleTimeout : MaxRenewalInterval);

        return Task.CompletedTask;
    }

    public static DateTimeOffset? RenewedAt(HttpContext context) =>
        context.Items.TryGetValue(RenewedAtItem, out var renewedAt) ? (DateTimeOffset?)renewedAt : null;

    public static DateTimeOffset? SignedInAt(AuthenticationProperties properties) =>
        properties.Items.TryGetValue(SignedInAtItem, out var stamp)
        && DateTimeOffset.TryParseExact(stamp, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var signedInAt)
            ? signedInAt
            : null;

    private Task<bool> IsInItsEntraSessionAsync(ClaimsPrincipal principal, string accountId, CancellationToken cancellationToken) =>
        principal.FindFirst(JwtRegisteredClaimNames.Sid)?.Value is { Length: > 0 } entraSessionId
            ? entraSessions.HoldsAsync(entraSessionId, accountId, cancellationToken)
            : Task.FromResult(true);

    private static SignedInPerson PersonOf(ClaimsPrincipal principal)
    {
        var objectId = SignInAudit.ObjectIdOf(principal) ?? throw new InvalidOperationException("A session starts only for a principal with an Entra object id.");
        var userName = principal.FindFirstValue(ClaimConstants.PreferredUserName) ?? string.Empty;

        return new SignedInPerson(objectId, principal.FindFirstValue(ClaimConstants.Name) ?? userName, userName);
    }

    private Task<bool> IsAdmittedAsync(ClaimsPrincipal principal, CancellationToken cancellationToken) =>
        SignInAudit.ObjectIdOf(principal) is { } objectId ? admission.IsAdmittedAsync(objectId, cancellationToken) : Task.FromResult(false);

    private async Task<bool> IsAccountCachedAsync(string accountId)
    {
        var application = await applications.GetConfidentialClientApplicationAsync(OpenIdConnectDefaults.AuthenticationScheme).ConfigureAwait(false);

        return await application.GetAccountAsync(accountId).ConfigureAwait(false) is not null;
    }
}
