using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Application.Queries.ListDesignations;

internal sealed record ListDesignationsQuery(ListRequest Request) : IQuery<PagedResult<DesignationDetails>>;
