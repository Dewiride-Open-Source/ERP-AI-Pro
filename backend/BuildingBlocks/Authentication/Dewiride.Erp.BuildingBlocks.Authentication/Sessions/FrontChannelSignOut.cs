using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web.Extensibility;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Sessions;

// Signs the person out the way POST /api/auth/logout does, from the Entra session their sign-in recorded (EntraSessions): the
// sign-out record first, written whatever happens to the request meanwhile, so a record that cannot be written leaves the
// person signed in and a written one refuses every older session of the person even if removing their account fails; then
// their account in the token cache. An Entra session id reaches only Entra and the API, in the id token and inside the
// encrypted session cookie, so a request that names no recorded session, or another issuer, changes nothing. No log record
// names the session, the issuer or the account.
internal sealed partial class FrontChannelSignOut(
    EntraSessions entraSessions,
    SessionRevocations revocations,
    IConfidentialClientApplicationProvider applications,
    ILogger<FrontChannelSignOut> logger)
{
    public async Task SignOutAsync(string issuer, string sessionId, CancellationToken cancellationToken)
    {
        var session = await entraSessions.FindAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            LogUnknownSession(logger);
            return;
        }

        if (!string.Equals(session.Issuer, issuer, StringComparison.Ordinal))
        {
            LogOtherIssuer(logger);
            return;
        }

        await revocations.RevokeAsync(session.AccountId, CancellationToken.None).ConfigureAwait(false);
        var application = await applications.GetConfidentialClientApplicationAsync(OpenIdConnectDefaults.AuthenticationScheme).ConfigureAwait(false);
        if (await application.GetAccountAsync(session.AccountId).ConfigureAwait(false) is { } account)
        {
            await application.RemoveAsync(account).ConfigureAwait(false);
        }

        await entraSessions.ForgetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        LogSignedOut(logger);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ended every session of a person whose Microsoft Entra ID session was signed out through the front channel")]
    private static partial void LogSignedOut(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A front-channel sign-out named a recorded Microsoft Entra ID session with another issuer, so nobody was signed out")]
    private static partial void LogOtherIssuer(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "A front-channel sign-out named no Microsoft Entra ID session this API recorded")]
    private static partial void LogUnknownSession(ILogger logger);
}
