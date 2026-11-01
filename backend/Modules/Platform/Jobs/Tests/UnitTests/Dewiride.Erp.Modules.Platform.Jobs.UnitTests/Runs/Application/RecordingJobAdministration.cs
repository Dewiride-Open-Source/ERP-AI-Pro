using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Jobs.UnitTests.Runs.Application;

internal sealed class RecordingJobAdministration : IJobAdministration
{
    private readonly List<string> _calls = [];

    public IReadOnlyList<JobDetails> Jobs { get; init; } = [];

    public Result<PagedResult<JobRunDetails>> Runs { get; init; } = new PagedResult<JobRunDetails>([], 1, 50, 0);

    public Result<JobRunHistory> History { get; init; } = JobErrors.RunNotFound;

    public Result<JobRunDetails> Rerun { get; init; } = JobErrors.RunNotFound;

    public Result<JobRunDetails> Cancel { get; init; } = JobErrors.RunNotFound;

    public IReadOnlyList<string> Calls => _calls;

    public ListRequest? ReceivedRequest { get; private set; }

    public JobRunId? ReceivedId { get; private set; }

    public CancellationToken ReceivedCancellation { get; private set; }

    public Task<IReadOnlyList<JobDetails>> ListJobsAsync(CancellationToken cancellationToken)
    {
        Record(nameof(ListJobsAsync), cancellationToken);

        return Task.FromResult(Jobs);
    }

    public Task<Result<PagedResult<JobRunDetails>>> ListRunsAsync(ListRequest request, CancellationToken cancellationToken)
    {
        Record(nameof(ListRunsAsync), cancellationToken);
        ReceivedRequest = request;

        return Task.FromResult(Runs);
    }

    public Task<Result<JobRunHistory>> GetRunAsync(JobRunId id, CancellationToken cancellationToken)
    {
        Record(nameof(GetRunAsync), cancellationToken);
        ReceivedId = id;

        return Task.FromResult(History);
    }

    public Task<Result<JobRunDetails>> RerunAsync(JobRunId id, CancellationToken cancellationToken)
    {
        Record(nameof(RerunAsync), cancellationToken);
        ReceivedId = id;

        return Task.FromResult(Rerun);
    }

    public Task<Result<JobRunDetails>> CancelAsync(JobRunId id, CancellationToken cancellationToken)
    {
        Record(nameof(CancelAsync), cancellationToken);
        ReceivedId = id;

        return Task.FromResult(Cancel);
    }

    private void Record(string call, CancellationToken cancellationToken)
    {
        _calls.Add(call);
        ReceivedCancellation = cancellationToken;
    }
}
