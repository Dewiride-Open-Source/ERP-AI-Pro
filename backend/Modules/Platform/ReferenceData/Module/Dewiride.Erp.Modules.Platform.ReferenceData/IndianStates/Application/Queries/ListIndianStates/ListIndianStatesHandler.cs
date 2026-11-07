using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Application.Queries.ListIndianStates;

internal sealed class ListIndianStatesHandler(IIndianStates states) : IQueryHandler<ListIndianStatesQuery, IReadOnlyList<IndianStateEntry>>
{
    public async Task<Result<IReadOnlyList<IndianStateEntry>>> HandleAsync(ListIndianStatesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Result.Success(await states.ListAsync(query.On, cancellationToken).ConfigureAwait(false));
    }
}
