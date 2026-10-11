using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application.Commands.WithdrawPosition;

internal sealed record WithdrawPositionCommand(Guid PersonId, Guid PositionId) : ICommand<Guid>;
