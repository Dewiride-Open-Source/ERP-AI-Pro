using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Application.Queries.GetParameterSet;

// Without a date the version shown is the one in force today in India; a date before the set's first version shows none.
internal sealed class GetParameterSetHandler(IStatutoryParameterHistory history, TimeProvider timeProvider) : IQueryHandler<GetParameterSetQuery, ShownParameterSet>
{
    public async Task<Result<ShownParameterSet>> HandleAsync(GetParameterSetQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var today = IndianStandardTime.Today(timeProvider);
        if (await history.GetSetAsync(query.Key, today, cancellationToken).ConfigureAwait(false) is not { } set)
        {
            return ParameterSetErrors.NotFound;
        }

        return new ShownParameterSet(set, await history.GetVersionAsync(query.Key, query.On ?? today, cancellationToken).ConfigureAwait(false));
    }
}
