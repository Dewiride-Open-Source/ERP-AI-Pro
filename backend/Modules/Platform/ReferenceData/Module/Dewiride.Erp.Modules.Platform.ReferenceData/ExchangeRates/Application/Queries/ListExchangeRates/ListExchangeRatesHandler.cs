using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Queries;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Queries.ListExchangeRates;

// The newest day first, so the rates a person checks are on the first page.
internal sealed class ListExchangeRatesHandler(ReferenceDataDbContext context) : IQueryHandler<ListExchangeRatesQuery, PagedResult<ExchangeRateDetails>>
{
    private static readonly SortableFields<ExchangeRate> Sortable = new SortableFields<ExchangeRate>()
        .Add("rateDate", e => e.RateDate)
        .Add("currency", e => e.Currency);

    private static readonly FilterableFields<ExchangeRate> Filterable = new FilterableFields<ExchangeRate>()
        .Add("currency", e => e.Currency)
        .Add("rateDate", e => e.RateDate);

    private static readonly SortRequest NewestFirst = new([new SortTerm("rateDate", SortDirection.Descending), new SortTerm("currency", SortDirection.Ascending)]);

    public async Task<Result<PagedResult<ExchangeRateDetails>>> HandleAsync(ListExchangeRatesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sort = (query.Request.Sort.Terms.Count == 0 ? NewestFirst : query.Request.Sort).Resolve(Sortable);
        if (sort.IsFailure)
        {
            return sort.Error!;
        }

        var filter = query.Request.Filter.Resolve(Filterable);
        if (filter.IsFailure)
        {
            return filter.Error!;
        }

        var page = await context.ExchangeRates
            .AsNoTracking()
            .ApplyFilter(filter.Value)
            .ApplySort(sort.Value, e => e.Id)
            .ToPagedResultAsync(query.Request.Page, cancellationToken)
            .ConfigureAwait(false);

        return page.Map(ExchangeRateDetails.From);
    }
}
