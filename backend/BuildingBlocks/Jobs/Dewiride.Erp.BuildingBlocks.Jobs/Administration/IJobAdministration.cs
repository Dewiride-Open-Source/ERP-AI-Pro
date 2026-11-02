using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Administration;

public interface IJobAdministration
{
    Task<IReadOnlyList<JobDetails>> ListJobsAsync(CancellationToken cancellationToken);

    Task<Result<PagedResult<JobRunDetails>>> ListRunsAsync(ListRequest request, CancellationToken cancellationToken);

    Task<Result<JobRunHistory>> GetRunAsync(JobRunId id, CancellationToken cancellationToken);

    Task<Result<JobRunDetails>> RerunAsync(JobRunId id, CancellationToken cancellationToken);

    Task<Result<JobRunDetails>> CancelAsync(JobRunId id, CancellationToken cancellationToken);
}
