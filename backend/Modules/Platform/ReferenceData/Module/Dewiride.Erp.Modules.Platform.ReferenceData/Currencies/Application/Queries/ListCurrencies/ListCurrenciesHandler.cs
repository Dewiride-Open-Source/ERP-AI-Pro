using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Currencies.Application.Queries.ListCurrencies;

// The currencies are the kernel's, the ones Money can hold, so the list has no table of its own to drift from them.
internal sealed class ListCurrenciesHandler : IQueryHandler<ListCurrenciesQuery, IReadOnlyList<Currency>>
{
    public Task<Result<IReadOnlyList<Currency>>> HandleAsync(ListCurrenciesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Task.FromResult(Result.Success(Currency.All));
    }
}
