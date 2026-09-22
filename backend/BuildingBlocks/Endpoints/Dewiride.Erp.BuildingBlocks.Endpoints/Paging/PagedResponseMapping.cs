using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Paging;

public static class PagedResponseMapping
{
    public static PagedResponse<T> ToResponse<T>(this PagedResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new PagedResponse<T>(result.Items, result.Page, result.PageSize, result.TotalCount, result.TotalPages);
    }

    public static PagedResponse<TResponse> ToResponse<T, TResponse>(this PagedResult<T> result, Func<T, TResponse> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(map);

        return result.Map(map).ToResponse();
    }
}
