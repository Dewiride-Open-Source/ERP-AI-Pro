namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public PagedResult<TOther> Map<TOther>(Func<T, TOther> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return new PagedResult<TOther>(Items.Select(map).ToArray(), Page, PageSize, TotalCount);
    }
}
