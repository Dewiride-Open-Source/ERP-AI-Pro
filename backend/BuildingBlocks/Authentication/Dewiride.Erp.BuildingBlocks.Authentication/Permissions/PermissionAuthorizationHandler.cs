using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Permissions;

// A route that names a permission no module declares would refuse every caller, administrators included, so it fails the
// request loudly instead. A refusal is marked on the request, so the bearer scheme's forbid tells a missing permission from a
// missing scope.
internal sealed class PermissionAuthorizationHandler(PermissionCatalog catalog) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (!catalog.Contains(requirement.Permission))
        {
            throw new InvalidOperationException($"A route requires the permission '{requirement.Permission}', which no module declares.");
        }

        if (context.Resource is not HttpContext request)
        {
            return Task.CompletedTask;
        }

        if (catalog.PermissionsOf(CallerRoles.Of(request)).Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
        else
        {
            PermissionRefusals.Mark(request);
        }

        return Task.CompletedTask;
    }
}
