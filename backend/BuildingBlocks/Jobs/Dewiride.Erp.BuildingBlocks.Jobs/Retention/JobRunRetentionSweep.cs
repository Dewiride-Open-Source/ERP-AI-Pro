using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Retention;

// Finished runs leave once they are older than the retention, together with their events, which the database deletes with their
// run; a run still pending, running or failed stays whatever its age. The sweep runs in the migrator with the other sweeps, so
// every retention sweep runs in one step on one timer. Runs go oldest first in statements of BatchSize, each committed on its
// own: every run takes its events along, and a statement that locks 5,000 rows of one index makes SQL Server lock the whole
// table instead, which would hold back the running API. The schedule rows of jobs this release does not schedule go too.
internal sealed partial class JobRunRetentionSweep(
    JobsDbContext context,
    JobCatalog catalog,
    TimeProvider timeProvider,
    IOptions<JobsOptions> options,
    ILogger<JobRunRetentionSweep> logger) : IRetentionSweep
{
    public const int BatchSize = 100;

    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow() - options.Value.FinishedRunRetention;
        var finished = context.Runs
            .Where(r => (r.Status == JobRunStatus.Succeeded || r.Status == JobRunStatus.DeadLettered || r.Status == JobRunStatus.Cancelled) && r.FinishedAt < cutoff)
            .OrderBy(r => r.FinishedAt)
            .ThenBy(r => r.Id);
        long runs = 0;
        int deleted;
        do
        {
            deleted = await finished.Take(BatchSize).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            runs += deleted;
        }
        while (deleted == BatchSize);

        LogRunsDeleted(runs, cutoff);

        var scheduled = catalog.Recurring.Select(registration => registration.Name).ToArray();
        var schedules = await context.Schedules.Where(s => !scheduled.Contains(s.JobName)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        LogSchedulesDeleted(schedules);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} job runs that finished before {Cutoff}, with their events.")]
    private partial void LogRunsDeleted(long count, DateTimeOffset cutoff);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted the schedules of {Count} jobs this release does not schedule.")]
    private partial void LogSchedulesDeleted(int count);
}
