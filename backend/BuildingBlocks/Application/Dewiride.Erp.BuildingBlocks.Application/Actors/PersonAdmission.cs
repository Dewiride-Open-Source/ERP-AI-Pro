using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.BuildingBlocks.Application.Actors;

public sealed record PersonAdmission(IReadOnlySet<ErpRole> AssignedRoles);
