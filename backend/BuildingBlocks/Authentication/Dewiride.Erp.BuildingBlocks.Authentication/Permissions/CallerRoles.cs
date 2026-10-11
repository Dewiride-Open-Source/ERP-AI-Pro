using System.Collections.Frozen;
using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Dewiride.Erp.BuildingBlocks.Authentication.SecurityEvents;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Microsoft.AspNetCore.Http;
using Microsoft.Identity.Web;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Permissions;

// A person holds the roles of their app roles, which their session's ID token or their own access token carries in roles,
// and the roles assigned to their record, which the accepted session or token put on the request. An application's roles
// are its own application roles, never a person's, so an application holds no built-in role.
internal static class CallerRoles
{
    public static IReadOnlySet<ErpRole> Of(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var caller = context.User;
        if (caller.Identity?.IsAuthenticated != true || BearerTokenClaims.HolderOf(caller) is BearerTokenHolder.Application)
        {
            return FrozenSet<ErpRole>.Empty;
        }

        var roles = new HashSet<ErpRole>();
        foreach (var appRole in caller.FindAll(ClaimConstants.Roles).Select(claim => claim.Value))
        {
            if (string.Equals(appRole, AppRoles.Administrator, StringComparison.Ordinal))
            {
                roles.Add(ErpRole.Administrator);
            }
            else if (string.Equals(appRole, AppRoles.User, StringComparison.Ordinal))
            {
                roles.Add(ErpRole.Employee);
            }
        }

        if (context.Features.Get<PersonRolesFeature>() is { } assigned && SignInAudit.ObjectIdOf(caller) == assigned.ObjectId)
        {
            roles.UnionWith(assigned.AssignedRoles);
        }

        return roles;
    }

    public static void KeepAssignedRoles(HttpContext context, ClaimsPrincipal person, PersonAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(admission);

        if (SignInAudit.ObjectIdOf(person) is { } objectId)
        {
            context.Features.Set(new PersonRolesFeature(objectId, admission.AssignedRoles));
        }
    }
}
