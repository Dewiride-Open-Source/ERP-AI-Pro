using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Dewiride.Erp.BuildingBlocks.Jobs.Running;
using Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.Testing.Jobs;

// Drives the real engine of a host whose loop does not run. A run is claimed by its id, never as the next due run, because every
// host of the test process shares one database and the next due run may be another test's.
public sealed class JobRunnerDriver(IServiceProvider services)
{
    public async Task<JobRunOutcome> RunAsync(JobRunId runId, CancellationToken cancellationToken)
    {
        var attempt = await services.GetRequiredService<JobRunner>().TryRunAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"The job run {runId.Value} is not due, is held by another runner, or belongs to a job this host does not define.");
        var status = attempt.RecordedStatus ?? await StatusOfAsync(runId, cancellationToken);

        return new JobRunOutcome(status, attempt.Outcome.Error is { } error ? StoredError.From(error).Code : null, attempt.Run.TraceId);
    }

    public Task<int> EnqueueDueOccurrencesAsync(CancellationToken cancellationToken) =>
        services.GetRequiredService<JobScheduler>().EnqueueDueAsync(cancellationToken);

    public Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken) =>
        services.GetRequiredService<JobRunner>().RecoverExpiredLeasesAsync(cancellationToken);

    private async Task<JobRunStatus> StatusOfAsync(JobRunId runId, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<JobsDbContext>().Runs
            .AsNoTracking()
            .Where(r => r.Id == runId)
            .Select(r => r.Status)
            .SingleAsync(cancellationToken);
    }
}
