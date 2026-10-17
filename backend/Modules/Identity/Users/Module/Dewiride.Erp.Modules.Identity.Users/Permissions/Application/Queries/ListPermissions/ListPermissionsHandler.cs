using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Modules;

namespace Dewiride.Erp.Modules.Identity.Users.Permissions.Application.Queries.ListPermissions;

// The roles are built in and what each allows is the modules' declarations, so the catalogue the host built at startup is the
// whole answer; nothing about roles is stored.
internal sealed class ListPermissionsHandler(PermissionCatalog catalog) : IQueryHandler<ListPermissionsQuery, PermissionCatalogDetails>
{
    public Task<Result<PermissionCatalogDetails>> HandleAsync(ListPermissionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Task.FromResult(Result.Success(new PermissionCatalogDetails(Enum.GetValues<ErpRole>(), catalog.Permissions)));
    }
}
