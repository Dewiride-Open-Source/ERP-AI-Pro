using System.Security.Claims;

namespace Dewiride.Erp.Testing.Authentication;

public sealed record TestUser(Guid ObjectId, string Name, string UserName, IReadOnlyList<string> Roles)
{
    public const string ObjectIdClaim = "oid";

    public const string TenantIdClaim = "tid";

    public const string HomeObjectIdClaim = "uid";

    public const string HomeTenantIdClaim = "utid";

    public const string NameClaim = "name";

    public const string UserNameClaim = "preferred_username";

    public const string RoleClaim = "roles";

    public string AccountId => $"{ObjectId:D}.{TestIdentityProvider.TenantId}";

    public ClaimsPrincipal ToPrincipal(string authenticationType)
    {
        List<Claim> claims =
        [
            new(ObjectIdClaim, ObjectId.ToString("D")),
            new(TenantIdClaim, TestIdentityProvider.TenantId),
            new(HomeObjectIdClaim, ObjectId.ToString("D")),
            new(HomeTenantIdClaim, TestIdentityProvider.TenantId),
            new(NameClaim, Name),
            new(UserNameClaim, UserName),
            .. Roles.Select(role => new Claim(RoleClaim, role)),
        ];

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType, UserNameClaim, RoleClaim));
    }
}
