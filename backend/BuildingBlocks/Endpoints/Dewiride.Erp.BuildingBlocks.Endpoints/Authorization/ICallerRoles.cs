using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;

// For an endpoint that shows callers the built-in roles they hold, counted as their permissions are: the roles their app
// roles give and those assigned to their record in the ERP.
public interface ICallerRoles
{
    IReadOnlySet<ErpRole> Roles { get; }
}
