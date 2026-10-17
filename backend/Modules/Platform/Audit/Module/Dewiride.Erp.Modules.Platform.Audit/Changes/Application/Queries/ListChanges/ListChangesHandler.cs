using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Audit.Changes.Application.Queries.ListChanges;

internal sealed class ListChangesHandler(IChangeHistory history) : IQueryHandler<ListChangesQuery, PagedResult<ChangeRecordDetails>>
{
    public Task<Result<PagedResult<ChangeRecordDetails>>> HandleAsync(ListChangesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return history.ListAsync(query.Request, cancellationToken);
    }
}
