using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Queries.ListJobRuns;

internal sealed class ListJobRunsHandler(IJobAdministration administration) : IQueryHandler<ListJobRunsQuery, PagedResult<JobRunDetails>>
{
    public Task<Result<PagedResult<JobRunDetails>>> HandleAsync(ListJobRunsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return administration.ListRunsAsync(query.Request, cancellationToken);
    }
}
