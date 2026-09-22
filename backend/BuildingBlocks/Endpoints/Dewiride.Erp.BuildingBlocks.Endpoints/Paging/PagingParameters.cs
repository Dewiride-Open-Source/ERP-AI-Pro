using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Paging;

public sealed record PagingParameters(int? Page, int? PageSize, string? Sort, string? Filter)
{
    public Result<ListRequest> ToListRequest() => ListRequest.Parse(Page, PageSize, Sort, Filter);
}
