using System.Collections.Concurrent;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Jobs.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;

// Enqueues the occurrences of the recurring jobs, reading each schedule from its module's options at every poll. A job seen for
// the first time, or whose expression changed, starts from the next occurrence and runs nothing in the past. After the API was
// down only the latest missed occurrence runs, once: the jobs need today's state, and a deployment or the nightly restart that
// spans an occurrence must not leave that night out. An occurrence is skipped while the run of the previous one is unfinished,
// so a stuck or retrying run never piles up. An invalid schedule stops only its own job, never the API, because nothing would
// tell anyone the API had not started; it is logged once for each value it takes.
internal sealed partial class JobScheduler(
    IServiceScopeFactory scopes,
    JobCatalog catalog,
    JobSignal signal,
    JobsMetrics metrics,
    TimeProvider time,
    ILogger<JobScheduler> logger)
{
    private const string Unreadable = "\0unreadable";

    private readonly ConcurrentDictionary<string, string> _reported = new(StringComparer.Ordinal);

    public async Task<int> EnqueueDueAsync(CancellationToken cancellationToken)
    {
        if (catalog.Recurring.Count == 0)
        {
            return 0;
        }

        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
        var enqueued = 0;
        foreach (var registration in catalog.Recurring)
        {
            if (Read(registration, scope.ServiceProvider) is { } schedule && await EnqueueDueAsync(context, registration, schedule, cancellationToken).ConfigureAwait(false))
            {
                enqueued++;
            }
        }

        if (enqueued > 0)
        {
            signal.Wake();
        }

        return enqueued;
    }

    private CronSchedule? Read(JobRegistration registration, IServiceProvider services)
    {
        string? text;
        try
        {
            text = registration.Schedule!(services);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (Report(registration.Name, Unreadable))
            {
                LogScheduleUnreadable(exception, registration.Name);
            }

            return null;
        }

        var parsed = CronSchedule.Parse(text);
        if (parsed.IsFailure)
        {
            if (Report(registration.Name, text ?? string.Empty))
            {
                LogScheduleInvalid(registration.Name, text ?? string.Empty, parsed.Error!.Message);
            }

            return null;
        }

        _reported.TryRemove(registration.Name, out _);

        return parsed.Value;
    }

    private bool Report(string jobName, string value)
    {
        var reported = _reported.TryGetValue(jobName, out var previous) && string.Equals(previous, value, StringComparison.Ordinal);
        _reported[jobName] = value;

        return !reported;
    }

    private async Task<bool> EnqueueDueAsync(JobsDbContext context, JobRegistration registration, CronSchedule schedule, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var row = await context.Schedules.AsNoTracking().SingleOrDefaultAsync(s => s.JobName == registration.Name, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            var first = schedule.NextAfter(now);
            if (await JobScheduleTransitions.TryStartAsync(context, JobSchedule.Start(registration.Name, schedule.Expression, first), cancellationToken).ConfigureAwait(false))
            {
                LogScheduled(registration.Name, schedule.Expression, first);
            }

            return false;
        }

        if (!string.Equals(row.Expression, schedule.Expression, StringComparison.Ordinal))
        {
            var next = schedule.NextAfter(now);
            if (await JobScheduleTransitions.TryChangeAsync(context, row, schedule.Expression, next, cancellationToken).ConfigureAwait(false))
            {
                LogScheduleChanged(registration.Name, row.Expression, schedule.Expression, next);
            }

            return false;
        }

        if (row.NextOccurrenceAt > now)
        {
            return false;
        }

        var occurrence = row.NextOccurrenceAt;
        for (var later = schedule.NextAfter(occurrence); later <= now; later = schedule.NextAfter(later))
        {
            occurrence = later;
        }

        var run = JobRun.Enqueue(JobRunId.Create(), registration.Name, OccurrenceKeys.For(occurrence), null, occurrence, now, ActorIds.System);
        var enqueued = JobRunEvent.Record(run.Id, JobRunEventKind.Enqueued, now, ActorIds.System);
        var outcome = await JobScheduleTransitions.TryEnqueueAsync(context, row, occurrence, schedule.NextAfter(occurrence), run, enqueued, cancellationToken).ConfigureAwait(false);
        switch (outcome)
        {
            case OccurrenceOutcome.Enqueued:
                metrics.OccurrenceEnqueued(registration.Name);
                LogOccurrenceEnqueued(registration.Name, occurrence);
                return true;
            case OccurrenceOutcome.Skipped:
                metrics.OccurrenceSkipped(registration.Name);
                LogOccurrenceSkipped(registration.Name, occurrence);
                return false;
            default:
                return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduled {JobName} at {Expression}; its first occurrence is {NextOccurrenceAt}")]
    private partial void LogScheduled(string jobName, string expression, DateTimeOffset nextOccurrenceAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "The schedule of {JobName} changed from {PreviousExpression} to {Expression}; its next occurrence is {NextOccurrenceAt}")]
    private partial void LogScheduleChanged(string jobName, string previousExpression, string expression, DateTimeOffset nextOccurrenceAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enqueued the occurrence of {JobName} at {OccurrenceAt}")]
    private partial void LogOccurrenceEnqueued(string jobName, DateTimeOffset occurrenceAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped the occurrence of {JobName} at {OccurrenceAt}: the run of its previous occurrence is unfinished")]
    private partial void LogOccurrenceSkipped(string jobName, DateTimeOffset occurrenceAt);

    [LoggerMessage(Level = LogLevel.Error, Message = "The schedule '{Expression}' of {JobName} is invalid, so the job is not scheduled until it is fixed: {Problem}")]
    private partial void LogScheduleInvalid(string jobName, string expression, string problem);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The schedule of {JobName} could not be read from its options, so the job is not scheduled until it can be")]
    private partial void LogScheduleUnreadable(Exception exception, string jobName);
}
