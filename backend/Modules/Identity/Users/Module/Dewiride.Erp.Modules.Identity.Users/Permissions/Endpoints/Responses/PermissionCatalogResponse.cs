using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.Modules.Identity.Users.Permissions.Endpoints.Responses;

/// <summary>What each built-in role of the ERP may do.</summary>
/// <param name="Roles">Every built-in role, in the order the ERP lists them.</param>
/// <param name="Permissions">Every permission the modules declare, in ordinal order of its name.</param>
internal sealed record PermissionCatalogResponse(IReadOnlyList<ErpRole> Roles, IReadOnlyList<PermissionResponse> Permissions);
