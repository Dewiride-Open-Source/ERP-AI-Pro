using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;

public sealed record PageRequest
{
    public const int DefaultPageSize = 50;

    public const int MaxPageSize = 200;

    public static readonly PageRequest First = new(1, DefaultPageSize);

    public PageRequest(int page, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, MaxPage(pageSize));

        Page = page;
        PageSize = pageSize;
    }

    public int Page { get; }

    public int PageSize { get; }

    public int Skip => (Page - 1) * PageSize;

    private static int MaxPage(int pageSize) => (int)Math.Min(int.MaxValue, ((long)int.MaxValue / pageSize) + 1);

    public static Result<PageRequest> Create(int? page, int? pageSize)
    {
        var resolvedPage = page ?? 1;
        var resolvedSize = pageSize ?? DefaultPageSize;
        if (resolvedPage < 1)
        {
            return QueryErrors.Page("'page' starts at 1.");
        }

        if (resolvedSize is < 1 or > MaxPageSize)
        {
            return QueryErrors.Page($"'pageSize' is between 1 and {MaxPageSize}.");
        }

        if (resolvedPage > MaxPage(resolvedSize))
        {
            return QueryErrors.Page($"'page' is at most {MaxPage(resolvedSize)} for a page size of {resolvedSize}.");
        }

        return new PageRequest(resolvedPage, resolvedSize);
    }
}
