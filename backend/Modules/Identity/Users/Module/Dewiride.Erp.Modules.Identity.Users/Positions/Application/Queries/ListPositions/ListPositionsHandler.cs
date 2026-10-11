using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Queries;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application.Queries.ListPositions;

internal sealed class ListPositionsHandler(UsersDbContext context) : IQueryHandler<ListPositionsQuery, PagedResult<PositionDetails>>
{
    private static readonly SortableFields<PositionRow> Sortable = new SortableFields<PositionRow>()
        .Add("effectiveFrom", r => r.Position.EffectiveFrom);

    private static readonly FilterableFields<PositionRow> Filterable = new FilterableFields<PositionRow>()
        .Add("effectiveFrom", r => r.Position.EffectiveFrom);

    private static readonly SortRequest Latest = new([new SortTerm("effectiveFrom", SortDirection.Descending)]);

    public async Task<Result<PagedResult<PositionDetails>>> HandleAsync(ListPositionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sort = (query.Request.Sort.Terms.Count == 0 ? Latest : query.Request.Sort).Resolve(Sortable);
        if (sort.IsFailure)
        {
            return sort.Error!;
        }

        var filter = query.Request.Filter.Resolve(Filterable);
        if (filter.IsFailure)
        {
            return filter.Error!;
        }

        var person = UserId.From(query.PersonId);
        if (!await context.Users.AnyAsync(u => u.Id == person, cancellationToken).ConfigureAwait(false))
        {
            return UserErrors.NotFound;
        }

        var page = await PositionsWithNames.Of(context, person)
            .AsNoTracking()
            .ApplyFilter(filter.Value)
            .ApplySort(sort.Value, r => r.Position.Id)
            .ToPagedResultAsync(query.Request.Page, cancellationToken)
            .ConfigureAwait(false);

        return page.Map(PositionDetails.From);
    }
}
