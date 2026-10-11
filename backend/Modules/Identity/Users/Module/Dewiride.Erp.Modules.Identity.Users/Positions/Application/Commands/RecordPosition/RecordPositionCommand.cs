using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application.Commands.RecordPosition;

internal sealed record RecordPositionCommand(Guid PersonId, DateOnly EffectiveFrom, Guid? DepartmentId, Guid? DesignationId, Guid? ManagerId) : ICommand<Guid>;
