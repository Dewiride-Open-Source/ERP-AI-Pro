using Dewiride.Erp.BuildingBlocks.Application.Queries;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application.Queries.GetPosition;

internal sealed record GetPositionQuery(Guid PersonId, Guid PositionId) : IQuery<PositionDetails>;
