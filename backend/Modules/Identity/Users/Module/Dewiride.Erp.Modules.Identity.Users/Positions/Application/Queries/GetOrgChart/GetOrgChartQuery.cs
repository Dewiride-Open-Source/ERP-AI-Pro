using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application.Queries.GetOrgChart;

internal sealed record GetOrgChartQuery(DateOnly On, ListRequest Request) : IQuery<PagedResult<OrgChartPersonDetails>>;
