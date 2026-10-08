using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Dewiride.Erp.Testing.Authentication;

// The Entra session id and the login hint follow from the object id, so a person a test derives from a persona with a new
// object id has a session and a hint of their own; like Entra's, the hint is opaque and never the sign-in name.
public sealed record TestUser(Guid ObjectId, string Name, string UserName, IReadOnlyList<string> Roles)
{
    public const string ObjectIdClaim = "oid";

    public const string TenantIdClaim = "tid";

    public const string HomeObjectIdClaim = "uid";

    public const string HomeTenantIdClaim = "utid";

    public const string NameClaim = "name";

    public const string UserNameClaim = "preferred_username";

    public const string RoleClaim = "roles";

    public const string IssuerClaim = "iss";

    public const string EntraSessionIdClaim = "sid";

    public const string LoginHintClaim = "login_hint";

    public string AccountId => $"{ObjectId:D}.{TestIdentityProvider.TenantId}";

    public string EntraSessionId => new Guid(Derive("sid")[..16]).ToString("D");

    public string LoginHint => Convert.ToBase64String(Derive("login_hint"));

    public ClaimsPrincipal ToPrincipal(string authenticationType)
    {
        List<Claim> claims =
        [
            new(IssuerClaim, TestIdentityProvider.Issuer),
            new(ObjectIdClaim, ObjectId.ToString("D")),
            new(TenantIdClaim, TestIdentityProvider.TenantId),
            new(HomeObjectIdClaim, ObjectId.ToString("D")),
            new(HomeTenantIdClaim, TestIdentityProvider.TenantId),
            new(EntraSessionIdClaim, EntraSessionId),
            new(NameClaim, Name),
            new(UserNameClaim, UserName),
            new(LoginHintClaim, LoginHint),
            .. Roles.Select(role => new Claim(RoleClaim, role)),
        ];

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType, UserNameClaim, RoleClaim));
    }

    private byte[] Derive(string purpose) => SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}:{ObjectId:D}"));
}
