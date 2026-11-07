using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.Countries;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Application.Queries.ListCountries;

internal sealed class ListCountriesHandler(ICountries countries) : IQueryHandler<ListCountriesQuery, IReadOnlyList<CountryEntry>>
{
    public async Task<Result<IReadOnlyList<CountryEntry>>> HandleAsync(ListCountriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Result.Success(await countries.ListAsync(cancellationToken).ConfigureAwait(false));
    }
}
