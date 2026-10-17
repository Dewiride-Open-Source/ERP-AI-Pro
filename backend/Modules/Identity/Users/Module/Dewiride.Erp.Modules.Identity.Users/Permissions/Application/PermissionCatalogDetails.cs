using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;

namespace Dewiride.Erp.Modules.Identity.Users.Permissions.Application;

internal sealed record PermissionCatalogDetails(IReadOnlyList<ErpRole> Roles, IReadOnlyList<PermissionCatalogEntry> Permissions);
