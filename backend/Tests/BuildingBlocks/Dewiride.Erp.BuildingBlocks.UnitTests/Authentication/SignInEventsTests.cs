using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Authentication.OpenIdConnect;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication;

public sealed class SignInEventsTests
{
    private const string Description = "AADSTS70008: The provided authorization code or refresh token has expired due to inactivity.";

    private const string WebOrigin = "https://erp.example.com";

    private const string LoginHint = "O.aW50ZXJuYWwtb3BhcXVlLWhpbnQ=";

    private const string ReturnPath = "/platform/attachments";

    private const string SignInScheme = "Cookies";

    [Theory]
    [InlineData("identity-provider error", SignInEvents.IdentityProviderFailure, "access_denied")]
    [InlineData("identity-provider error of 65 characters", SignInEvents.IdentityProviderFailure, SignInEvents.UnrecognisedOAuthError)]
    [InlineData("identity-provider error outside ASCII", SignInEvents.IdentityProviderFailure, SignInEvents.UnrecognisedOAuthError)]
    [InlineData("identity-provider error that is empty", SignInEvents.IdentityProviderFailure, SignInEvents.NoOAuthError)]
    [InlineData("protocol failure without an error", SignInEvents.ProtocolFailure, SignInEvents.NoOAuthError)]
    [InlineData("correlation failure", SignInEvents.CallbackFailure, SignInEvents.NoOAuthError)]
    [InlineData("invalid signature", SignInEvents.TokenValidationFailure, SignInEvents.NoOAuthError)]
    [InlineData("code redemption refused", SignInEvents.CodeRedemptionFailure, "invalid_grant")]
    [InlineData("code redemption needing interaction", SignInEvents.CodeRedemptionFailure, "invalid_grant")]
    [InlineData("code redemption error of 65 characters", SignInEvents.CodeRedemptionFailure, SignInEvents.UnrecognisedOAuthError)]
    [InlineData("code redemption error outside ASCII", SignInEvents.CodeRedemptionFailure, SignInEvents.UnrecognisedOAuthError)]
    [InlineData("token cache unavailable", SignInEvents.TokenCacheFailure, SignInEvents.NoOAuthError)]
    [InlineData("unexpected exception", SignInEvents.UnexpectedFailure, SignInEvents.NoOAuthError)]
    [InlineData("no exception", SignInEvents.UnexpectedFailure, SignInEvents.NoOAuthError)]
    public void Describe_Failure_NamesItsCategoryAndOnlyAnErrorCodeOfTheOAuthGrammar(string failure, string category, string oAuthError)
    {
        var described = SignInEvents.Describe(FailureOf(failure));

        Assert.Equal((category, oAuthError), described);
    }

    [Fact]
    public void Describe_IdentityProviderErrorOfSixtyFourCharacters_NamesTheError()
    {
        var error = new string('a', 64);

        var described = SignInEvents.Describe(ProtocolFailure(error));

        Assert.Equal((SignInEvents.IdentityProviderFailure, error), described);
    }

    [Fact]
    public async Task RedirectToIdentityProviderForSignOut_PersonWithALoginHint_SendsItAsTheLogoutHint()
    {
        var context = SignOutContext(new Claim(SignInEvents.LoginHintClaim, LoginHint));

        await Events().RedirectToIdentityProviderForSignOut(context);

        Assert.Equal(LoginHint, context.ProtocolMessage.GetParameter(SignInEvents.LogoutHintParameter));
        Assert.Equal($"{WebOrigin}/api/auth/signout-callback-oidc", context.ProtocolMessage.PostLogoutRedirectUri);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RedirectToIdentityProviderForSignOut_PersonWithoutALoginHint_SendsNoLogoutHint(bool emptyClaim)
    {
        var context = emptyClaim ? SignOutContext(new Claim(SignInEvents.LoginHintClaim, string.Empty)) : SignOutContext();

        await Events().RedirectToIdentityProviderForSignOut(context);

        Assert.Null(context.ProtocolMessage.GetParameter(SignInEvents.LogoutHintParameter));
        Assert.DoesNotContain(SignInEvents.LogoutHintParameter, context.ProtocolMessage.CreateLogoutRequestUrl(), StringComparison.Ordinal);
        Assert.Equal($"{WebOrigin}/api/auth/signout-callback-oidc", context.ProtocolMessage.PostLogoutRedirectUri);
    }

    [Fact]
    public async Task TicketReceived_SessionIssued_SignsTheTicketInAndRedirectsToItsReturnPath()
    {
        var authentication = new RecordingAuthentication();
        var context = TicketContext(authentication, ReturnPath);

        await Events().TicketReceived(context);

        var signIn = Assert.Single(authentication.SignIns);
        Assert.Equal(SignInScheme, signIn.Scheme);
        Assert.Same(context.Principal, signIn.Principal);
        Assert.Same(context.Properties, signIn.Properties);
        Assert.True(context.Result?.Handled);
        Assert.Equal(ReturnPath, context.Response.Headers.Location.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task TicketReceived_SessionIssuedWithoutAReturnPath_RedirectsToTheStartPage(string? returnUri)
    {
        var context = TicketContext(new RecordingAuthentication(), returnUri);

        await Events().TicketReceived(context);

        Assert.True(context.Result?.Handled);
        Assert.Equal("/", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task TicketReceived_SessionThatCannotBeIssued_SignsOutAgainAndLandsOnTheSignInFailedPageWithTheFailureLogged()
    {
        var logger = new FakeLogger<SignInEvents>();
        var failure = new TimeoutException(Description);
        var authentication = new RecordingAuthentication { Failure = failure };
        var context = TicketContext(authentication, ReturnPath);

        await Events(logger).TicketReceived(context);

        Assert.True(context.Result?.Handled);
        Assert.Equal("/login?error=sign-in-failed", context.Response.Headers.Location.ToString());
        Assert.Equal([SignInScheme], authentication.SignOuts);
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal(SignInEvents.SessionFailure, record.GetStructuredStateValue("Failure"));
        Assert.Equal(SignInEvents.NoOAuthError, record.GetStructuredStateValue("OAuthError"));
        Assert.Same(failure, record.Exception);
        Assert.DoesNotContain(Description, record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TicketReceived_CallbackTheBrowserAbandoned_LetsItsCancellationThroughUnlogged()
    {
        var logger = new FakeLogger<SignInEvents>();
        var authentication = new RecordingAuthentication { Failure = new OperationCanceledException() };
        var context = TicketContext(authentication, ReturnPath);
        context.HttpContext.RequestAborted = new CancellationToken(canceled: true);

        await Assert.ThrowsAsync<OperationCanceledException>(() => Events(logger).TicketReceived(context));

        Assert.Null(context.Result);
        Assert.Empty(authentication.SignOuts);
        Assert.Empty(logger.Collector.GetSnapshot());
    }

    private static SignInEvents Events(ILogger<SignInEvents>? logger = null) =>
        new(Microsoft.Extensions.Options.Options.Create(new EntraSignInOptions { WebOrigin = WebOrigin }), logger ?? NullLogger<SignInEvents>.Instance);

    private static TicketReceivedContext TicketContext(RecordingAuthentication authentication, string? returnUri) =>
        new(
            new DefaultHttpContext { RequestServices = authentication },
            new AuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme, displayName: null, typeof(OpenIdConnectHandler)),
            new OpenIdConnectOptions { SignInScheme = SignInScheme },
            new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sid", "7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e4f")], OpenIdConnectDefaults.AuthenticationScheme)), OpenIdConnectDefaults.AuthenticationScheme))
        {
            ReturnUri = returnUri,
        };

    private static RedirectContext SignOutContext(params Claim[] claims) =>
        new(
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies")) },
            new AuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme, displayName: null, typeof(OpenIdConnectHandler)),
            new OpenIdConnectOptions(),
            new AuthenticationProperties())
        {
            ProtocolMessage = new OpenIdConnectMessage { IssuerAddress = "https://login.microsoftonline.com/5d7c3b9a-1e2f-4a6b-8c0d-9e8f7a6b5c4d/oauth2/v2.0/logout" },
        };

    private static Exception? FailureOf(string failure) =>
        failure switch
        {
            "identity-provider error" => ProtocolFailure("access_denied"),
            "identity-provider error of 65 characters" => ProtocolFailure(new string('a', 65)),
            "identity-provider error outside ASCII" => ProtocolFailure("accès_refusé"),
            "identity-provider error that is empty" => ProtocolFailure(string.Empty),
            "protocol failure without an error" => new OpenIdConnectProtocolException("The nonce does not match."),
            "correlation failure" => new AuthenticationFailureException("Correlation failed."),
            "invalid signature" => new SecurityTokenInvalidSignatureException("The signature is invalid."),
            "code redemption refused" => new MsalServiceException("invalid_grant", Description),
            "code redemption needing interaction" => new MsalUiRequiredException("invalid_grant", Description),
            "code redemption error of 65 characters" => new MsalServiceException(new string('b', 65), Description),
            "code redemption error outside ASCII" => new MsalServiceException("ungültig", Description),
            "token cache unavailable" => new TokenCacheUnavailableException(new TimeoutException(Description)),
            "unexpected exception" => new InvalidOperationException(Description),
            "no exception" => null,
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "No failure of that name."),
        };

    private static OpenIdConnectProtocolException ProtocolFailure(string error)
    {
        var exception = new OpenIdConnectProtocolException($"Message contains error: '{error}', error_description: '{Description}'.");
        exception.Data["error"] = error;
        exception.Data["error_description"] = Description;

        return exception;
    }

    private sealed class RecordingAuthentication : IAuthenticationService, IServiceProvider
    {
        public List<(string? Scheme, ClaimsPrincipal Principal, AuthenticationProperties? Properties)> SignIns { get; } = [];

        public Exception? Failure { get; init; }

        public List<string?> SignOuts { get; } = [];

        public object? GetService(Type serviceType) => serviceType == typeof(IAuthenticationService) ? this : null;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            if (Failure is not null)
            {
                return Task.FromException(Failure);
            }

            SignIns.Add((scheme, principal, properties));

            return Task.CompletedTask;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => throw new NotSupportedException("A received ticket is only signed in.");

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException("A received ticket is only signed in.");

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException("A received ticket is only signed in.");

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOuts.Add(scheme);

            return Task.CompletedTask;
        }
    }
}
