using System.Diagnostics;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Jobs.Running;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.Testing.Jobs;

// Inserts a run in the state a test or an end-to-end spec needs, with the events the engine would have written on the way there,
// one second apart and ending now, so its history reads in order. A running run holds a lease of a day, which no runner of the
// test hosts recovers, and a failed one is due again an hour from now.
public static class JobRunSeeder
{
    public static readonly TimeSpan RunningLease = TimeSpan.FromDays(1);

    public static readonly TimeSpan RetryDelay = TimeSpan.FromHours(1);

    public static async Task<JobRunId> SeedAsync(IServiceProvider services, JobRunSeed seed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(seed);
        EnsureValid(seed);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        var attempts = seed.Status is JobRunStatus.Pending or JobRunStatus.Cancelled ? 0 : seed.Attempts;
        var error = StoredError.From(Error.Failure(seed.ErrorCode, seed.ErrorMessage));
        var runner = JobRunnerInstance.ForThisProcess().Name;
        var steps = Steps(seed, attempts);
        var start = now - TimeSpan.FromSeconds(steps.Count - 1);
        var run = JobRun.Enqueue(JobRunId.Create(), seed.JobName, seed.IdempotencyKey, null, start, start, seed.RequestedBy);
        var events = new List<JobRunEvent>();
        var traces = new Dictionary<int, string>();
        DateTimeOffset? startedAt = null;
        for (var index = 0; index < steps.Count; index++)
        {
            var (kind, attempt) = steps[index];
            var at = start + TimeSpan.FromSeconds(index);
            var actor = kind is JobRunEventKind.Enqueued or JobRunEventKind.CancelRequested or JobRunEventKind.Cancelled ? seed.RequestedBy : ActorIds.System;
            string? traceId = null;
            if (attempt is { } number && !traces.TryGetValue(number, out traceId))
            {
                traceId = ActivityTraceId.CreateRandom().ToHexString();
                traces[number] = traceId;
            }

            if (kind == JobRunEventKind.Started)
            {
                startedAt = at;
            }

            events.Add(kind switch
            {
                JobRunEventKind.Failed => JobRunEvent.Record(run.Id, kind, at, actor, attempt, runner, traceId, error, index + 1 < steps.Count ? at + TimeSpan.FromSeconds(1) : now + RetryDelay),
                JobRunEventKind.DeadLettered => JobRunEvent.Record(run.Id, kind, at, actor, attempt, runner, traceId, error),
                _ => JobRunEvent.Record(run.Id, kind, at, actor, attempt, attempt is null ? null : runner, traceId),
            });
        }

        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(
            async token =>
            {
                context.ChangeTracker.Clear();
                await using var transaction = await context.Database.BeginTransactionAsync(token);
                context.Runs.Add(run);
                context.Events.AddRange(events);
                await context.SaveChangesAsync(token);
                await context.Runs.Where(r => r.Id == run.Id).ExecuteUpdateAsync(setters => State(setters, seed, attempts, error, startedAt, now, runner), token);
                await transaction.CommitAsync(token);
            },
            cancellationToken);

        return run.Id;
    }

    private static void EnsureValid(JobRunSeed seed)
    {
        if (!JobNames.IsWellFormed(seed.JobName))
        {
            throw new ArgumentException($"'{seed.JobName}' is not a job name: three lower-case kebab segments.", nameof(seed));
        }

        if (!JobKeys.IsWellFormed(seed.IdempotencyKey))
        {
            throw new ArgumentException($"'{seed.IdempotencyKey}' is not an idempotency key.", nameof(seed));
        }

        if (!Enum.IsDefined(seed.Status))
        {
            throw new ArgumentException($"{seed.Status} is not a job run status.", nameof(seed));
        }

        if (seed.Attempts is < 1 or > JobRetryPolicy.AttemptsCeiling)
        {
            throw new ArgumentException($"A seeded run has started 1 to {JobRetryPolicy.AttemptsCeiling} attempts.", nameof(seed));
        }

        if (seed.StopRequested && seed.Status != JobRunStatus.Running)
        {
            throw new ArgumentException("Only a running run can have been asked to stop.", nameof(seed));
        }
    }

    private static List<(JobRunEventKind Kind, int? Attempt)> Steps(JobRunSeed seed, int attempts)
    {
        var steps = new List<(JobRunEventKind Kind, int? Attempt)> { (JobRunEventKind.Enqueued, null) };
        for (var attempt = 1; attempt < attempts; attempt++)
        {
            steps.Add((JobRunEventKind.Started, attempt));
            steps.Add((JobRunEventKind.Failed, attempt));
        }

        if (attempts > 0)
        {
            steps.Add((JobRunEventKind.Started, attempts));
        }

        switch (seed.Status)
        {
            case JobRunStatus.Running when seed.StopRequested:
                steps.Add((JobRunEventKind.CancelRequested, null));
                break;
            case JobRunStatus.Failed:
                steps.Add((JobRunEventKind.Failed, attempts));
                break;
            case JobRunStatus.Succeeded:
                steps.Add((JobRunEventKind.Succeeded, attempts));
                break;
            case JobRunStatus.DeadLettered:
                steps.Add((JobRunEventKind.DeadLettered, attempts));
                break;
            case JobRunStatus.Cancelled:
                steps.Add((JobRunEventKind.Cancelled, null));
                break;
        }

        return steps;
    }

    private static void State(
        UpdateSettersBuilder<JobRun> setters,
        JobRunSeed seed,
        int attempts,
        StoredError error,
        DateTimeOffset? startedAt,
        DateTimeOffset now,
        string runner)
    {
        var failures = seed.Status is JobRunStatus.Failed or JobRunStatus.DeadLettered ? attempts : Math.Max(attempts - 1, 0);
        setters
            .SetProperty(r => r.Status, seed.Status)
            .SetProperty(r => r.AttemptCount, attempts)
            .SetProperty(r => r.FailureCount, failures)
            .SetProperty(r => r.StartedAt, startedAt);
        switch (seed.Status)
        {
            case JobRunStatus.Pending:
                setters.SetProperty(r => r.DueAt, now);
                break;
            case JobRunStatus.Running:
                setters
                    .SetProperty(r => r.LeaseToken, (Guid?)Guid.CreateVersion7())
                    .SetProperty(r => r.LeaseHolder, runner)
                    .SetProperty(r => r.LeaseExpiresAt, (DateTimeOffset?)(now + RunningLease));
                if (seed.StopRequested)
                {
                    setters.SetProperty(r => r.CancelRequestedAt, (DateTimeOffset?)now).SetProperty(r => r.CancelRequestedBy, (Guid?)seed.RequestedBy);
                }

                break;
            case JobRunStatus.Failed:
                setters.SetProperty(r => r.DueAt, now + RetryDelay);
                break;
            case JobRunStatus.Cancelled:
                setters.SetProperty(r => r.CancelRequestedAt, (DateTimeOffset?)now).SetProperty(r => r.CancelRequestedBy, (Guid?)seed.RequestedBy);
                break;
        }

        if (seed.Status is JobRunStatus.Succeeded or JobRunStatus.DeadLettered or JobRunStatus.Cancelled)
        {
            setters.SetProperty(r => r.FinishedAt, (DateTimeOffset?)now);
        }

        if (failures > 0 && seed.Status != JobRunStatus.Succeeded)
        {
            setters.SetProperty(r => r.LastErrorCode, error.Code).SetProperty(r => r.LastErrorMessage, error.Message);
        }
    }
}
