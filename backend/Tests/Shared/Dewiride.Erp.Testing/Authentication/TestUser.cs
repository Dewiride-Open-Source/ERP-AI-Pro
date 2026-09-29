using System.Security.Claims;

namespace Dewiride.Erp.Testing.Authentication;

public sealed record TestUser(Guid ObjectId, string Name, string UserName, IReadOnlyList<string> Roles)
{
    public const string ObjectIdClaim = "oid";

    public const string TenantIdClaim = "tid";

    public const string NameClaim = "name";

    public const string UserNameClaim = "preferred_username";

    public const string RoleClaim = "roles";

    public ClaimsPrincipal ToPrincipal(string authenticationType)
    {
        List<Claim> claims =
        [
            new(ObjectIdClaim, ObjectId.ToString("D")),
            new(TenantIdClaim, TestIdentityProvider.TenantId),
            new(NameClaim, Name),
            new(UserNameClaim, UserName),
            .. Roles.Select(role => new Claim(RoleClaim, role)),
        ];

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType, UserNameClaim, RoleClaim));
    }
}
