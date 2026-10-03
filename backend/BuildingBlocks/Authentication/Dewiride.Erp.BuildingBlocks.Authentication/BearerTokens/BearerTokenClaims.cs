using System.Security.Claims;
using Microsoft.Identity.Web;

namespace Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;

// A Microsoft identity platform access token issued for a person carries the scopes it was granted in scp and the person's
// app roles in roles; one an application obtained for itself carries no scp and its application roles in roles, and says
// app in idtyp when the registration emits that optional claim, otherwise it is told apart by a sub equal to its oid
// (Microsoft Learn, "Protected web API: Verify scopes and app roles"). An id token of the same registration has the same
// audience, issuer and signing keys but neither scp nor a sub equal to its oid, so it has no holder.
internal static class BearerTokenClaims
{
    public const string TokenTypeClaim = "idtyp";

    public const string ApplicationTokenType = "app";

    public const string PersonTokenType = "user";

    public const string VersionClaim = "ver";

    public const string Version = "2.0";

    public static BearerTokenHolder? HolderOf(ClaimsPrincipal token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var tokenType = token.FindFirstValue(TokenTypeClaim);
        if (token.FindFirstValue(ClaimConstants.Scp) is not null)
        {
            return tokenType is null or PersonTokenType ? BearerTokenHolder.Person : null;
        }

        return tokenType switch
        {
            ApplicationTokenType => BearerTokenHolder.Application,
            null when token.FindFirstValue(ClaimConstants.Oid) is { } objectId && string.Equals(objectId, token.FindFirstValue(ClaimConstants.Sub), StringComparison.Ordinal) => BearerTokenHolder.Application,
            _ => null,
        };
    }

    public static IEnumerable<string> ScopesOf(ClaimsPrincipal token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return token.FindFirstValue(ClaimConstants.Scp)?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
    }

    public static IEnumerable<string> RolesOf(ClaimsPrincipal token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return token.FindAll(ClaimConstants.Roles).Select(role => role.Value);
    }
}
