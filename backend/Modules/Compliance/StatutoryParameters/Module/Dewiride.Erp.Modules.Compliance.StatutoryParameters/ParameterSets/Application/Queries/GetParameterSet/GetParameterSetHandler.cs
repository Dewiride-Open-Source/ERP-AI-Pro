using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Application.Queries.GetParameterSet;

// Without a date the version shown is the one in force today in India; a date before the set's first version shows none. A set
// with a GST State code column is shown with the names of its States, so the reference data is read only for those.
internal sealed class GetParameterSetHandler(IStatutoryParameterHistory history, IIndianStates indianStates, TimeProvider timeProvider) : IQueryHandler<GetParameterSetQuery, ShownParameterSet>
{
    public async Task<Result<ShownParameterSet>> HandleAsync(GetParameterSetQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var today = IndianStandardTime.Today(timeProvider);
        if (await history.GetSetAsync(query.Key, today, cancellationToken).ConfigureAwait(false) is not { } set)
        {
            return ParameterSetErrors.NotFound;
        }

        var shown = await history.GetVersionAsync(query.Key, query.On ?? today, cancellationToken).ConfigureAwait(false);
        var names = shown is not null && set.Columns.Any(column => column.Kind == ParameterColumnKind.StateCode)
            ? new StateNames(await indianStates.ListAsync(null, cancellationToken).ConfigureAwait(false))
            : StateNames.None;

        return new ShownParameterSet(set, shown, names);
    }
}
