using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.SecurityEvents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Sessions;

// Ends the sessions signed in from the Entra session the request names by removing its record (EntraSessions), and no other
// session of the person. The Entra session id is not secret: every application of the tenant signed in within that Entra
// session receives it in its id tokens and in its own front-channel sign-out, so a request naming it can end at most the
// sessions of that one Entra session, and never signs anyone in. Entra documents only that the request carries iss and sid,
// not which form of the issuer it sends, so both forms of this tenant's issuer are accepted: the v2.0 one of the id tokens
// and the v1.0 one Entra also issues workforce tokens under; any other changes nothing. Both store calls ignore the
// request's cancellation, so a frame Entra tears down mid-request still ends the sessions; the command timeout and the
// retry limits bound them. No log record names the session, the issuer or the account; the security event of a sign-out
// names the account by its object id in this tenant, and one naming another issuer is recorded with no account.
internal sealed partial class FrontChannelSignOut(EntraSessions entraSessions, IOptions<EntraSignInOptions> signIn, ILogger<FrontChannelSignOut> logger)
{
    public const string OtherIssuerDetail = "other-issuer";

    private const string V1IssuerBase = "https://sts.windows.net/";

    public async Task SignOutAsync(HttpContext context, string issuer, string sessionId)
    {
        if (!NamesThisTenant(issuer))
        {
            LogOtherIssuer(logger);
            await SignInAudit.TryRecordAsync(context, SecurityEventKind.FrontChannelSignOutRefused, OtherIssuerDetail).ConfigureAwait(false);
            return;
        }

        if (await entraSessions.FindAsync(sessionId, CancellationToken.None).ConfigureAwait(false) is not { } session)
        {
            LogUnknownSession(logger);
            return;
        }

        await entraSessions.ForgetAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
        LogSignedOut(logger);
        await SignInAudit.TryRecordAsync(context, SecurityEventKind.FrontChannelSignedOut, actor: ObjectIdOf(session.AccountId, signIn.Value.TenantId)).ConfigureAwait(false);
    }

    // The MSAL account id is the object id and the tenant id of the account's home tenant, joined by a dot. For a member of
    // this tenant that object id is the oid every other security event names; a guest's belongs to its account in another
    // tenant, so a guest's sign-out names no account rather than one no other record of the person carries.
    internal static Guid? ObjectIdOf(string accountId, string? tenantId)
    {
        var dot = accountId.IndexOf('.', StringComparison.Ordinal);
        if (dot < 0 || !string.Equals(accountId[(dot + 1)..], tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Guid.TryParse(accountId.AsSpan(0, dot), out var objectId) ? objectId : null;
    }

    private bool NamesThisTenant(string issuer) =>
        string.Equals(issuer, BearerTokenOptionsSetup.IssuerOf(signIn.Value), StringComparison.Ordinal)
        || string.Equals(issuer, $"{V1IssuerBase}{signIn.Value.TenantId}/", StringComparison.Ordinal);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ended the sessions of a Microsoft Entra ID session signed out through the front channel")]
    private static partial void LogSignedOut(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A front-channel sign-out named another issuer, so nobody was signed out")]
    private static partial void LogOtherIssuer(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "A front-channel sign-out named no Microsoft Entra ID session this API recorded")]
    private static partial void LogUnknownSession(ILogger logger);
}
