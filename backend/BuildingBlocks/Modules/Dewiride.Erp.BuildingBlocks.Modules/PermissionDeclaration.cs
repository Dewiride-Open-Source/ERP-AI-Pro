using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.BuildingBlocks.Modules;

// HeldBy names the built-in roles that hold the permission besides Administrator, which holds every permission and is
// therefore never named. Description is the one plain sentence the roles page shows to the people who assign roles: what a
// holder may do.
public sealed record PermissionDeclaration(string Name, IReadOnlyCollection<ErpRole> HeldBy, string Description);
