using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;

internal sealed class AllButAssigningRoles : ICallerPermissions
{
    public bool Holds(string permission) => !string.Equals(permission, UsersPermissions.AssignRoles, StringComparison.Ordinal);
}
