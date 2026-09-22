using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Queries;

public static class PagedQueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IOrderedQueryable<T> source, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(page);

        var total = await source.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = total > page.Skip
            ? await source.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false)
            : [];

        return new PagedResult<T>(items, page.Page, page.PageSize, total);
    }
}
