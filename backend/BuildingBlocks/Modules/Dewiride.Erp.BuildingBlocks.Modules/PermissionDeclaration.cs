using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.BuildingBlocks.Modules;

// HeldBy names the built-in roles that hold the permission besides Administrator, which holds every permission and is
// therefore never named.
public sealed record PermissionDeclaration(string Name, IReadOnlyCollection<ErpRole> HeldBy);
