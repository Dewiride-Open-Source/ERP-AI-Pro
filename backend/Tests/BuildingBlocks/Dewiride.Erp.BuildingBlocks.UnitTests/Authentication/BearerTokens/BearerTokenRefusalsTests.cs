using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.BearerTokens;

public sealed class BearerTokenRefusalsTests
{
    private static readonly Guid Application = Guid.Parse("3f2c1b0a-9e8d-4c7b-a6f5-e4d3c2b1a090");

    private static readonly Guid OtherApplication = Guid.Parse("8e7d6c5b-4a39-4281-b0f9-e8d7c6b5a4f3");

    [Theory]
    [InlineData("expired", "expired")]
    [InlineData("not yet valid", "not-yet-valid")]
    [InlineData("another audience", "invalid-audience")]
    [InlineData("another issuer", "invalid-issuer")]
    [InlineData("an unknown signing key", "unknown-signing-key")]
    [InlineData("an invalid signature", "invalid-signature")]
    [InlineData("another algorithm", "invalid-algorithm")]
    [InlineData("malformed", "malformed")]
    [InlineData("a segment that is not base64url", "malformed")]
    [InlineData("a refused argument", "malformed")]
    [InlineData("an invalid lifetime", "invalid")]
    [InlineData("no expiry", "invalid")]
    [InlineData("several failures", "validation-error")]
    [InlineData("an unexpected failure", "validation-error")]
    [InlineData("no failure", "validation-error")]
    public void Describe_Failure_NamesWhatWasWrongWithTheToken(string failure, string refusal)
    {
        Assert.Equal(refusal, BearerTokenRefusals.Describe(FailureOf(failure)));
    }

    [Fact]
    public void ClientApplicationOf_HolderNamingAnAuthorizedParty_IsThatApplication()
    {
        var holder = Holder(new Claim(BearerTokenRefusals.AuthorizedPartyClaim, Application.ToString()), new Claim(BearerTokenRefusals.ApplicationIdClaim, OtherApplication.ToString()));

        Assert.Equal(Application, BearerTokenRefusals.ClientApplicationOf(holder));
    }

    [Fact]
    public void ClientApplicationOf_HolderOfAVersionOneToken_IsItsApplicationId()
    {
        var holder = Holder(new Claim(BearerTokenRefusals.ApplicationIdClaim, Application.ToString()));

        Assert.Equal(Application, BearerTokenRefusals.ClientApplicationOf(holder));
    }

    [Fact]
    public void ClientApplicationOf_HolderNamingNoApplicationId_IsNoApplication()
    {
        Assert.Null(BearerTokenRefusals.ClientApplicationOf(Holder(new Claim(BearerTokenRefusals.AuthorizedPartyClaim, "erp-web"))));
        Assert.Null(BearerTokenRefusals.ClientApplicationOf(Holder()));
        Assert.Null(BearerTokenRefusals.ClientApplicationOf(null));
    }

    [Theory]
    [InlineData("Bearer")]
    [InlineData("bearer")]
    [InlineData("BEARER")]
    public void ClientApplicationClaimedBy_TokenNamingAnAuthorizedParty_IsThatApplicationUnverified(string scheme)
    {
        var request = Request($"{scheme} {Token($$"""{"azp":"{{Application}}","appid":"{{OtherApplication}}"}""")}");

        Assert.Equal(Application, BearerTokenRefusals.ClientApplicationClaimedBy(request));
    }

    [Fact]
    public void ClientApplicationClaimedBy_VersionOneToken_IsItsApplicationId()
    {
        var request = Request($"Bearer {Token($$"""{"appid":"{{Application}}"}""")}");

        Assert.Equal(Application, BearerTokenRefusals.ClientApplicationClaimedBy(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer ")]
    [InlineData("Bearer not-a-token")]
    [InlineData("Bearer a.b.c")]
    [InlineData("Bearer eyJhbGciOiJSU0EtT0FFUCIsImVuYyI6IkEyNTZHQ00ifQ.a.a.a.a")]
    [InlineData("Bearer e30.a.a.a.a")]
    [InlineData("Bearer eyJhbGciOiJSU0EtT0FFUCIsImVuYyI6IkEyNTZHQ00ifQ.AAAA.AAAA.AAAA.AAAA")]
    [InlineData("Bearer a.b.c.d.e.f")]
    [InlineData("Basic dXNlcjpwYXNzd29yZA==")]
    public void ClientApplicationClaimedBy_NoReadableBearerToken_IsNoApplication(string? authorization)
    {
        Assert.Null(BearerTokenRefusals.ClientApplicationClaimedBy(Request(authorization)));
    }

    [Theory]
    [InlineData("""{"sub":"someone"}""")]
    [InlineData("""{"azp":"erp-web"}""")]
    public void ClientApplicationClaimedBy_TokenNamingNoApplicationId_IsNoApplication(string payload)
    {
        Assert.Null(BearerTokenRefusals.ClientApplicationClaimedBy(Request($"Bearer {Token(payload)}")));
    }

    private static Exception? FailureOf(string failure) =>
        failure switch
        {
            "expired" => new SecurityTokenExpiredException("The token is expired."),
            "not yet valid" => new SecurityTokenNotYetValidException("The token is not yet valid."),
            "another audience" => new SecurityTokenInvalidAudienceException("The audience is invalid."),
            "another issuer" => new SecurityTokenInvalidIssuerException("The issuer is invalid."),
            "an unknown signing key" => new SecurityTokenSignatureKeyNotFoundException("The signing key was not found."),
            "an invalid signature" => new SecurityTokenInvalidSignatureException("The signature is invalid."),
            "another algorithm" => new SecurityTokenInvalidAlgorithmException("The algorithm is not allowed."),
            "malformed" => new SecurityTokenMalformedException("The token is malformed."),
            "a segment that is not base64url" => new FormatException("IDX10400: Unable to decode the encoded value."),
            "a refused argument" => new ArgumentException("IDX10209: The token is larger than the maximum size."),
            "an invalid lifetime" => new SecurityTokenInvalidLifetimeException("The lifetime is invalid."),
            "no expiry" => new SecurityTokenNoExpirationException("The token has no expiry."),
            "several failures" => new AggregateException(new SecurityTokenExpiredException("The token is expired."), new SecurityTokenInvalidSignatureException("The signature is invalid.")),
            "an unexpected failure" => new InvalidOperationException("The configuration could not be read."),
            "no failure" => null,
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "No failure of that name."),
        };

    private static ClaimsPrincipal Holder(params Claim[] claims) => new(new ClaimsIdentity(claims, "Bearer"));

    private static string Token(string payload) => new JsonWebTokenHandler().CreateToken(payload);

    private static HttpRequest Request(string? authorization)
    {
        var request = new DefaultHttpContext().Request;
        if (authorization is not null)
        {
            request.Headers.Authorization = authorization;
        }

        return request;
    }
}
