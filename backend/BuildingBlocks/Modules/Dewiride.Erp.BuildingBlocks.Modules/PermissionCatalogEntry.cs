using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.BuildingBlocks.Modules;

// HeldBy lists every built-in role that holds the permission, in the order of ErpRole, so Administrator is always among them.
public sealed record PermissionCatalogEntry(string Name, string Description, IReadOnlyList<ErpRole> HeldBy);
