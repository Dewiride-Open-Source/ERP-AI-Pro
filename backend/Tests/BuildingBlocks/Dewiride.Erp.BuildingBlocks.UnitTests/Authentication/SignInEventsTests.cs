using Dewiride.Erp.BuildingBlocks.Authentication.OpenIdConnect;
using Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication;

public sealed class SignInEventsTests
{
    private const string Description = "AADSTS70008: The provided authorization code or refresh token has expired due to inactivity.";

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
}
