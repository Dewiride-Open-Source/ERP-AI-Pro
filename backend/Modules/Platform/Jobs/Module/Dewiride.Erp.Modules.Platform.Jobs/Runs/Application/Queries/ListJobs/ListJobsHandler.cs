using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Queries.ListJobs;

internal sealed class ListJobsHandler(IJobAdministration administration) : IQueryHandler<ListJobsQuery, IReadOnlyList<JobDetails>>
{
    public async Task<Result<IReadOnlyList<JobDetails>>> HandleAsync(ListJobsQuery query, CancellationToken cancellationToken) =>
        Result.Success(await administration.ListJobsAsync(cancellationToken).ConfigureAwait(false));
}
