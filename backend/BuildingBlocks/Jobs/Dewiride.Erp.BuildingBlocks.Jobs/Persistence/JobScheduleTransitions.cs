using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Persistence;

// A schedule row changes only by compare-and-set on the next occurrence and expression the scheduler read, so of several
// schedulers polling at once exactly one takes each occurrence: the row's update lock makes the others wait, and they then find
// it moved on. Whether the previous run is unfinished is read only once the row is locked, and the unique key of the run is the
// last guard against a second run of one occurrence.
internal static class JobScheduleTransitions
{
    private const int UniqueIndexViolation = 2601;

    private const int UniqueConstraintViolation = 2627;

    public static async Task<bool> TryStartAsync(JobsDbContext context, JobSchedule schedule, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        context.Schedules.Add(schedule);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            return false;
        }
        finally
        {
            context.Entry(schedule).State = EntityState.Detached;
        }
    }

    public static async Task<bool> TryChangeAsync(JobsDbContext context, JobSchedule observed, string expression, DateTimeOffset nextOccurrenceAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observed);

        var changed = await Observed(context, observed)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Expression, expression).SetProperty(s => s.NextOccurrenceAt, nextOccurrenceAt), cancellationToken)
            .ConfigureAwait(false);

        return changed == 1;
    }

    public static Task<OccurrenceOutcome> TryEnqueueAsync(
        JobsDbContext context,
        JobSchedule observed,
        DateTimeOffset occurrence,
        DateTimeOffset nextOccurrenceAt,
        JobRun run,
        JobRunEvent enqueued,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observed);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(enqueued);

        return context.Database.CreateExecutionStrategy().ExecuteAsync(
            async token =>
            {
                context.ChangeTracker.Clear();
                await using var transaction = await context.Database.BeginTransactionAsync(token).ConfigureAwait(false);
                var advanced = await Observed(context, observed)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.NextOccurrenceAt, nextOccurrenceAt), token)
                    .ConfigureAwait(false);
                if (advanced == 0)
                {
                    return await context.Runs.AnyAsync(r => r.Id == run.Id, token).ConfigureAwait(false) ? OccurrenceOutcome.Enqueued : OccurrenceOutcome.Taken;
                }

                var lastRunId = await context.Schedules
                    .AsNoTracking()
                    .Where(s => s.JobName == observed.JobName)
                    .Select(s => s.LastRunId)
                    .SingleAsync(token)
                    .ConfigureAwait(false);
                var unfinished = lastRunId is { } previous && await context.Runs
                    .AnyAsync(r => r.Id == previous && (r.Status == JobRunStatus.Pending || r.Status == JobRunStatus.Running || r.Status == JobRunStatus.Failed), token)
                    .ConfigureAwait(false);
                if (unfinished)
                {
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return OccurrenceOutcome.Skipped;
                }

                if (await context.Runs.AnyAsync(r => r.JobName == run.JobName && r.IdempotencyKey == run.IdempotencyKey, token).ConfigureAwait(false))
                {
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return OccurrenceOutcome.Taken;
                }

                context.Runs.Add(run);
                context.Events.Add(enqueued);
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                await context.Schedules
                    .Where(s => s.JobName == observed.JobName)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.LastOccurrenceAt, (DateTimeOffset?)occurrence).SetProperty(s => s.LastRunId, (JobRunId?)run.Id), token)
                    .ConfigureAwait(false);
                await transaction.CommitAsync(token).ConfigureAwait(false);

                return OccurrenceOutcome.Enqueued;
            },
            cancellationToken);
    }

    private static IQueryable<JobSchedule> Observed(JobsDbContext context, JobSchedule observed) =>
        context.Schedules.Where(s => s.JobName == observed.JobName && s.Expression == observed.Expression && s.NextOccurrenceAt == observed.NextOccurrenceAt);
}
