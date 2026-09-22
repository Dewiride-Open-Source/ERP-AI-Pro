using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;

public sealed record ListRequest(PageRequest Page, SortRequest Sort, FilterRequest Filter)
{
    public static readonly ListRequest Default = new(PageRequest.First, SortRequest.None, FilterRequest.None);

    public static Result<ListRequest> Parse(int? page, int? pageSize, string? sort, string? filter)
    {
        var pageRequest = PageRequest.Create(page, pageSize);
        if (pageRequest.IsFailure)
        {
            return pageRequest.Error!;
        }

        var sortRequest = SortRequest.Parse(sort);
        if (sortRequest.IsFailure)
        {
            return sortRequest.Error!;
        }

        var filterRequest = FilterRequest.Parse(filter);
        if (filterRequest.IsFailure)
        {
            return filterRequest.Error!;
        }

        return new ListRequest(pageRequest.Value, sortRequest.Value, filterRequest.Value);
    }
}
