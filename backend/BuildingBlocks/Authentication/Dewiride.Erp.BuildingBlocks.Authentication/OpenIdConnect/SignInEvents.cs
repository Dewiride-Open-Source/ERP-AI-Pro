using System.Text.RegularExpressions;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
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
// every handler here completes before it returns. A failure is logged by category and OAuth error code only: the error
// description, the query and the posted form can carry personal data or tokens.
internal sealed partial class SignInEvents(IOptions<EntraSignInOptions> signIn, ILogger<SignInEvents> logger)
{
    public const string IdentityProviderFailure = "identity-provider";

    public const string ProtocolFailure = "protocol";

    public const string CallbackFailure = "callback";

    public const string TokenValidationFailure = "token-validation";

    public const string CodeRedemptionFailure = "code-redemption";

    public const string TokenCacheFailure = "token-cache";

    public const string UnexpectedFailure = "unexpected";

    public const string NoOAuthError = "none";

    public const string UnrecognisedOAuthError = "unrecognised";

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

        return Task.CompletedTask;
    }

    public Task RemoteFailure(RemoteFailureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (failure, oAuthError) = Describe(context.Failure);
        LogSignInFailed(logger, failure, oAuthError);
        context.HandleResponse();
        context.Response.Redirect(AuthPaths.SignInFailedPage);

        return Task.CompletedTask;
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
}
