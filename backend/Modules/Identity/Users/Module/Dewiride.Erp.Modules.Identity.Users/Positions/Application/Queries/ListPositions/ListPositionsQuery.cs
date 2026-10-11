using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application.Queries.ListPositions;

internal sealed record ListPositionsQuery(Guid PersonId, ListRequest Request) : IQuery<PagedResult<PositionDetails>>;
