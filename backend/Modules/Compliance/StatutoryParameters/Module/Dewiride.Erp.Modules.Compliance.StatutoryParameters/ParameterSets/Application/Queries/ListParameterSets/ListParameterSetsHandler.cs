using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Application.Queries.ListParameterSets;

// A version starts on a date in India, so today is India's date.
internal sealed class ListParameterSetsHandler(IStatutoryParameterHistory history, TimeProvider timeProvider) : IQueryHandler<ListParameterSetsQuery, IReadOnlyList<ParameterSetEntry>>
{
    public async Task<Result<IReadOnlyList<ParameterSetEntry>>> HandleAsync(ListParameterSetsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Result.Success(await history.ListSetsAsync(IndianStandardTime.Today(timeProvider), cancellationToken).ConfigureAwait(false));
    }
}
