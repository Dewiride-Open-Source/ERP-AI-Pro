using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;

// A refusal is recorded by what was wrong with the token, never by its content. The client application of a token that
// failed validation is the one its payload claims, unverified, because nothing in such a token can be trusted.
internal static class BearerTokenRefusals
{
    public const string AuthorizedPartyClaim = "azp";

    public const string ApplicationIdClaim = "appid";

    private const string BearerPrefix = "Bearer ";

    public static string Describe(Exception? failure) =>
        failure switch
        {
            SecurityTokenExpiredException => "expired",
            SecurityTokenNotYetValidException => "not-yet-valid",
            SecurityTokenInvalidAudienceException => "invalid-audience",
            SecurityTokenInvalidIssuerException => "invalid-issuer",
            SecurityTokenSignatureKeyNotFoundException => "unknown-signing-key",
            SecurityTokenInvalidSignatureException => "invalid-signature",
            SecurityTokenInvalidAlgorithmException => "invalid-algorithm",
            SecurityTokenMalformedException => "malformed",
            SecurityTokenException => "invalid",
            _ => "validation-error",
        };

    public static Guid? ClientApplicationOf(ClaimsPrincipal? holder) =>
        ApplicationIdOf(holder?.FindFirstValue(AuthorizedPartyClaim) ?? holder?.FindFirstValue(ApplicationIdClaim));

    public static Guid? ClientApplicationClaimedBy(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var token = new JsonWebToken(header[BearerPrefix.Length..].Trim());

            return token.TryGetPayloadValue<string>(AuthorizedPartyClaim, out var application) || token.TryGetPayloadValue(ApplicationIdClaim, out application)
                ? ApplicationIdOf(application)
                : null;
        }
        catch (Exception exception) when (exception is ArgumentException or SecurityTokenMalformedException)
        {
            return null;
        }
    }

    private static Guid? ApplicationIdOf(string? value) => Guid.TryParse(value, out var applicationId) ? applicationId : null;
}
