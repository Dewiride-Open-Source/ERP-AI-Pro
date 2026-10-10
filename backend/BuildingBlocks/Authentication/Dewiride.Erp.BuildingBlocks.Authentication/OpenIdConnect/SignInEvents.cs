using System.Text.RegularExpressions;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.SecurityEvents;
using Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Dewiride.Erp.BuildingBlocks.Authentication.OpenIdConnect;

// Microsoft.Identity.Web chains these handlers into multicast delegates, which await only the task of the last one, so
// every handler here completes before it returns, except TicketReceived and RemoteFailure: Microsoft.Identity.Web chains
// those only after the event's default handler, which does nothing, so their tasks are the ones awaited. TicketReceived
// issues the session itself, so a session that cannot be started lands on the sign-in failed page like any failed
// callback instead of on an error page, and signs out again whatever the cookie handler wrote before it failed; a session
// is started only once its sign-in is recorded. A failed callback is logged and recorded by category and OAuth error code
// only: the error description, the query and the posted form can carry personal data or tokens. A session that cannot be
// started is logged with its exception, because only the Entra session record, the ticket's protection, the antiforgery
// tokens and the sign-in record fail there, and none of their exceptions names a token or the account; a callback the
// browser abandoned is no failed sign-in, so its cancellation is left to the request pipeline.
internal sealed partial class SignInEvents(IOptions<EntraSignInOptions> signIn, ILogger<SignInEvents> logger)
{
    public const string IdentityProviderFailure = "identity-provider";

    public const string ProtocolFailure = "protocol";

    public const string CallbackFailure = "callback";

    public const string TokenValidationFailure = "token-validation";

    public const string CodeRedemptionFailure = "code-redemption";

    public const string TokenCacheFailure = "token-cache";

    public const string SessionFailure = "session";

    public const string UnexpectedFailure = "unexpected";

    public const string NoOAuthError = "none";

    public const string UnrecognisedOAuthError = "unrecognised";

    public const string LoginHintClaim = "login_hint";

    public const string LogoutHintParameter = "logout_hint";

    private const string OAuthErrorKey = "error";

    public Task RedirectToIdentityProvider(RedirectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.ProtocolMessage.RedirectUri = signIn.Value.WebOrigin + AuthPaths.SignInCallback;

        return Task.CompletedTask;
    }

    public Task RedirectToIdentityProviderForSignOut(RedirectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.ProtocolMessage.PostLogoutRedirectUri = signIn.Value.WebOrigin + AuthPaths.SignedOutCallback;

        // The login_hint optional claim names the account, so Entra signs it out without asking which account to sign out.
        if (context.HttpContext.User.FindFirst(LoginHintClaim)?.Value is { Length: > 0 } loginHint)
        {
            context.ProtocolMessage.SetParameter(LogoutHintParameter, loginHint);
        }

        return Task.CompletedTask;
    }

    public async Task RemoteFailure(RemoteFailureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (failure, oAuthError) = Describe(context.Failure);
        LogSignInFailed(logger, failure, oAuthError);
        await SignInAudit.TryRecordAsync(context.HttpContext, SecurityEventKind.SignInFailed, FailureDetail(failure, oAuthError)).ConfigureAwait(false);
        context.HandleResponse();
        context.Response.Redirect(AuthPaths.SignInFailedPage);
    }

    public async Task TicketReceived(TicketReceivedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var httpContext = context.HttpContext;
        var person = SignInAudit.ObjectIdOf(context.Principal);
        try
        {
            await httpContext.SignInAsync(context.Options.SignInScheme, context.Principal!, context.Properties).ConfigureAwait(false);
            await SignInAudit.RecordAsync(httpContext, SecurityEventKind.SignedIn, actor: person).ConfigureAwait(false);
        }
        catch (Exception exception) when (!httpContext.RequestAborted.IsCancellationRequested)
        {
            await httpContext.SignOutAsync(context.Options.SignInScheme).ConfigureAwait(false);
            LogSessionNotStarted(logger, exception, SessionFailure, NoOAuthError);
            await SignInAudit.TryRecordAsync(httpContext, SecurityEventKind.SignInFailed, FailureDetail(SessionFailure, NoOAuthError), person).ConfigureAwait(false);
            context.Response.Redirect(AuthPaths.SignInFailedPage);
            context.HandleResponse();

            return;
        }

        context.Response.Redirect(string.IsNullOrEmpty(context.ReturnUri) ? "/" : context.ReturnUri);
        context.HandleResponse();
    }

    public static (string Failure, string OAuthError) Describe(Exception? failure) =>
        failure switch
        {
            OpenIdConnectProtocolException protocol when protocol.Data[OAuthErrorKey] is string error => (IdentityProviderFailure, OAuthErrorCode(error)),
            OpenIdConnectProtocolException => (ProtocolFailure, NoOAuthError),
            AuthenticationFailureException => (CallbackFailure, NoOAuthError),
            SecurityTokenException => (TokenValidationFailure, NoOAuthError),
            MsalException msal => (CodeRedemptionFailure, OAuthErrorCode(msal.ErrorCode)),
            TokenCacheUnavailableException => (TokenCacheFailure, NoOAuthError),
            _ => (UnexpectedFailure, NoOAuthError),
        };

    public static string FailureDetail(string failure, string oAuthError) => $"{failure}:{oAuthError}";

    // RFC 6749 section 5.2 error codes are short ASCII words, so any other value did not come from Entra and is not logged.
    private static string OAuthErrorCode(string? error) =>
        error switch
        {
            null or "" => NoOAuthError,
            _ when OAuthErrorPattern().IsMatch(error) => error,
            _ => UnrecognisedOAuthError,
        };

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex OAuthErrorPattern();

    [LoggerMessage(Level = LogLevel.Warning, Message = "A sign-in did not complete: {Failure} failure with OAuth error {OAuthError}")]
    private static partial void LogSignInFailed(ILogger logger, string failure, string oAuthError);

    [LoggerMessage(Level = LogLevel.Error, Message = "A sign-in did not complete: {Failure} failure with OAuth error {OAuthError}")]
    private static partial void LogSessionNotStarted(ILogger logger, Exception exception, string failure, string oAuthError);
}
