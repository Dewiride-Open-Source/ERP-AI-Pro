using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Running.Attempts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Persistence;

// Every change of a run after its insert is one of these compare-and-set statements: an UPDATE of one row whose WHERE names the
// state it expects, with the event that records the change inserted in the same transaction. No row means a runner or an
// administrator changed the run first, and nothing is written. The statements of an attempt also name its lease token, so a
// runner whose lease was taken over can never overwrite the attempt that replaced it. Without optimized locking a READ
// COMMITTED UPDATE qualifies its row under an update lock, under row versioning too; under Azure SQL's optimized locking it
// qualifies the latest committed version without one, waits for a writer that holds the row and qualifies it again. Either way
// two runners cannot both see the state they expect. A stop request wins over every ending of an attempt but success: a
// failure, a dead letter or an interruption is written only while the run holds no request, and otherwise the attempt ends
// cancelled in the same transaction, so no waiting run keeps a request that its next claim would ignore. The retrying execution
// strategy runs a transition again after a commit whose acknowledgement was lost; the retry then finds the event that commit
// wrote and reports the change it made. A claim that committed is read back whatever the shutdown does, so its attempt runs as
// any other in flight, ending on its own or interrupted when the shutdown's timeout fires, instead of the run waiting under a
// lease nobody renews.
internal static class JobRunTransitions
{
    public static Task<List<JobRun>> DueAsync(JobsDbContext context, IReadOnlyCollection<string> jobNames, DateTimeOffset now, int take, CancellationToken cancellationToken) =>
        context.Runs
            .AsNoTracking()
            .Where(r => (r.Status == JobRunStatus.Pending || r.Status == JobRunStatus.Failed) && r.DueAt <= now && jobNames.Contains(r.JobName))
            .OrderBy(r => r.DueAt)
            .ThenBy(r => r.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    public static Task<List<JobRun>> AbandonedAsync(JobsDbContext context, IReadOnlyCollection<string> jobNames, DateTimeOffset now, int take, CancellationToken cancellationToken) =>
        context.Runs
            .AsNoTracking()
            .Where(r => r.Status == JobRunStatus.Running && r.LeaseExpiresAt <= now && jobNames.Contains(r.JobName))
            .OrderBy(r => r.LeaseExpiresAt)
            .ThenBy(r => r.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    public static async Task<ClaimedRun?> TryClaimAsync(JobsDbContext context, JobRunId runId, int observedAttempts, LeaseClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);

        var started = JobRunEvent.Record(runId, JobRunEventKind.Started, claim.At, ActorIds.System, observedAttempts + 1, claim.Runner, claim.TraceId);
        var claimed = await InTransactionAsync(
            context,
            started,
            token => context.Runs
                .Where(r => r.Id == runId
                    && (r.Status == JobRunStatus.Pending || r.Status == JobRunStatus.Failed)
                    && r.DueAt <= claim.At
                    && r.AttemptCount == observedAttempts)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(r => r.Status, JobRunStatus.Running)
                        .SetProperty(r => r.LeaseToken, (Guid?)claim.Token)
                        .SetProperty(r => r.LeaseHolder, claim.Runner)
                        .SetProperty(r => r.LeaseExpiresAt, (DateTimeOffset?)claim.ExpiresAt)
                        .SetProperty(r => r.StartedAt, (DateTimeOffset?)claim.At)
                        .SetProperty(r => r.AttemptCount, r => r.AttemptCount + 1),
                    token),
            cancellationToken).ConfigureAwait(false);
        if (!claimed)
        {
            return null;
        }

        var run = await context.Runs.AsNoTracking().SingleOrDefaultAsync(r => r.Id == runId && r.LeaseToken == claim.Token, CancellationToken.None).ConfigureAwait(false);

        return run is null ? null : new ClaimedRun(run.Id, run.JobName, run.Payload, run.AttemptCount, run.FailureCount, claim.Token, claim.Runner, claim.TraceId, claim.At);
    }

    public static async Task<LeaseState> RenewAsync(JobsDbContext context, JobRunId runId, Guid leaseToken, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var renewed = await Held(context, runId, leaseToken)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.LeaseExpiresAt, (DateTimeOffset?)expiresAt), cancellationToken)
            .ConfigureAwait(false);
        if (renewed == 0)
        {
            return LeaseState.Lost;
        }

        var cancelRequested = await context.Runs
            .AsNoTracking()
            .Where(r => r.Id == runId)
            .Select(r => r.CancelRequestedAt != null)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return cancelRequested ? LeaseState.CancelRequested : LeaseState.Held;
    }

    public static Task<JobRunStatus?> EndAttemptAsync(JobsDbContext context, ClaimedRun run, DateTimeOffset at, AttemptEnding ending, JobRunEvent recorded, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        var stopped = JobRunEvent.Record(run.Id, JobRunEventKind.Cancelled, at, ActorIds.System, run.Attempt, run.Runner, run.TraceId);

        return EndAsync(context, Held(context, run.Id, run.LeaseToken), at, ending, recorded, stopped, cancellationToken);
    }

    public static Task<JobRunStatus?> EndAbandonedAsync(JobsDbContext context, JobRun observed, DateTimeOffset now, AttemptEnding ending, JobRunEvent recorded, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observed);

        var stopped = JobRunEvent.Record(observed.Id, JobRunEventKind.Cancelled, now, ActorIds.System, observed.AttemptCount, observed.LeaseHolder, error: StoredError.From(JobAttemptErrors.LeaseExpired));

        return EndAsync(context, Abandoned(context, observed, now), now, ending, recorded, stopped, cancellationToken);
    }

    public static Task<bool> RerunAsync(JobsDbContext context, JobRunId runId, DateTimeOffset at, Guid actorId, CancellationToken cancellationToken) =>
        InTransactionAsync(
            context,
            JobRunEvent.Record(runId, JobRunEventKind.Rerun, at, actorId),
            token => context.Runs
                .Where(r => r.Id == runId && (r.Status == JobRunStatus.Failed || r.Status == JobRunStatus.DeadLettered || r.Status == JobRunStatus.Cancelled))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(r => r.Status, JobRunStatus.Pending)
                        .SetProperty(r => r.DueAt, at)
                        .SetProperty(r => r.FailureCount, 0)
                        .SetProperty(r => r.FinishedAt, (DateTimeOffset?)null)
                        .SetProperty(r => r.LastErrorCode, (string?)null)
                        .SetProperty(r => r.LastErrorMessage, (string?)null)
                        .SetProperty(r => r.CancelRequestedAt, (DateTimeOffset?)null)
                        .SetProperty(r => r.CancelRequestedBy, (Guid?)null),
                    token),
            cancellationToken);

    public static Task<bool> CancelWaitingAsync(JobsDbContext context, JobRunId runId, DateTimeOffset at, Guid actorId, CancellationToken cancellationToken) =>
        InTransactionAsync(
            context,
            JobRunEvent.Record(runId, JobRunEventKind.Cancelled, at, actorId),
            token => context.Runs
                .Where(r => r.Id == runId && (r.Status == JobRunStatus.Pending || r.Status == JobRunStatus.Failed))
                .ExecuteUpdateAsync(setters => Cancel(setters, at, actorId), token),
            cancellationToken);

    public static Task<bool> CancelAbandonedAsync(JobsDbContext context, JobRun observed, DateTimeOffset at, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observed);

        var error = StoredError.From(JobAttemptErrors.LeaseExpired);

        return InTransactionAsync(
            context,
            JobRunEvent.Record(observed.Id, JobRunEventKind.Cancelled, at, actorId, observed.AttemptCount, observed.LeaseHolder, error: error),
            token => Abandoned(context, observed, at)
                .ExecuteUpdateAsync(
                    setters =>
                    {
                        Cancel(setters, at, actorId);
                        ReleaseLease(setters);
                    },
                    token),
            cancellationToken);
    }

    public static Task<bool> RequestCancelAsync(JobsDbContext context, JobRunId runId, DateTimeOffset at, Guid actorId, CancellationToken cancellationToken) =>
        InTransactionAsync(
            context,
            JobRunEvent.Record(runId, JobRunEventKind.CancelRequested, at, actorId),
            token => context.Runs
                .Where(r => r.Id == runId && r.Status == JobRunStatus.Running && r.CancelRequestedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(r => r.CancelRequestedAt, (DateTimeOffset?)at)
                        .SetProperty(r => r.CancelRequestedBy, (Guid?)actorId),
                    token),
            cancellationToken);

    private static IQueryable<JobRun> Held(JobsDbContext context, JobRunId runId, Guid leaseToken) =>
        context.Runs.Where(r => r.Id == runId && r.LeaseToken == leaseToken && r.Status == JobRunStatus.Running);

    private static IQueryable<JobRun> Abandoned(JobsDbContext context, JobRun observed, DateTimeOffset now) =>
        Held(context, observed.Id, observed.LeaseToken ?? Guid.Empty).Where(r => r.LeaseExpiresAt <= now);

    private static async Task<JobRunStatus?> EndAsync(
        JobsDbContext context,
        IQueryable<JobRun> attempt,
        DateTimeOffset at,
        AttemptEnding ending,
        JobRunEvent recorded,
        JobRunEvent stopped,
        CancellationToken cancellationToken)
    {
        if (ending.Status is JobRunStatus.Succeeded or JobRunStatus.Cancelled)
        {
            return await InTransactionAsync(context, recorded, token => End(attempt, ending, token), cancellationToken).ConfigureAwait(false) ? ending.Status : null;
        }

        var cancelled = AttemptEnding.Cancelled(at);
        var written = await FirstInTransactionAsync(
            context,
            [
                (recorded, token => End(attempt.Where(r => r.CancelRequestedAt == null), ending, token)),
                (stopped, token => End(attempt.Where(r => r.CancelRequestedAt != null), cancelled, token)),
            ],
            cancellationToken).ConfigureAwait(false);

        return written switch
        {
            0 => ending.Status,
            1 => cancelled.Status,
            _ => null,
        };
    }

    private static Task<int> End(IQueryable<JobRun> attempt, AttemptEnding ending, CancellationToken cancellationToken) =>
        attempt.ExecuteUpdateAsync(
            setters =>
            {
                setters.SetProperty(r => r.Status, ending.Status);
                ReleaseLease(setters);
                if (ending.DueAt is { } dueAt)
                {
                    setters.SetProperty(r => r.DueAt, dueAt);
                }

                if (ending.FailureCount is { } failures)
                {
                    setters.SetProperty(r => r.FailureCount, failures);
                }

                if (ending.FinishedAt is { } finishedAt)
                {
                    setters.SetProperty(r => r.FinishedAt, (DateTimeOffset?)finishedAt);
                }

                if (ending.Error is { } error)
                {
                    setters.SetProperty(r => r.LastErrorCode, error.Code).SetProperty(r => r.LastErrorMessage, error.Message);
                }
                else if (ending.ClearsError)
                {
                    setters.SetProperty(r => r.LastErrorCode, (string?)null).SetProperty(r => r.LastErrorMessage, (string?)null);
                }
            },
            cancellationToken);

    private static void Cancel(UpdateSettersBuilder<JobRun> setters, DateTimeOffset at, Guid actorId) =>
        setters
            .SetProperty(r => r.Status, JobRunStatus.Cancelled)
            .SetProperty(r => r.FinishedAt, (DateTimeOffset?)at)
            .SetProperty(r => r.CancelRequestedAt, (DateTimeOffset?)at)
            .SetProperty(r => r.CancelRequestedBy, (Guid?)actorId);

    private static void ReleaseLease(UpdateSettersBuilder<JobRun> setters) =>
        setters
            .SetProperty(r => r.LeaseToken, (Guid?)null)
            .SetProperty(r => r.LeaseHolder, (string?)null)
            .SetProperty(r => r.LeaseExpiresAt, (DateTimeOffset?)null);

    private static async Task<bool> InTransactionAsync(JobsDbContext context, JobRunEvent recorded, Func<CancellationToken, Task<int>> change, CancellationToken cancellationToken) =>
        await FirstInTransactionAsync(context, [(recorded, change)], cancellationToken).ConfigureAwait(false) == 0;

    private static Task<int> FirstInTransactionAsync(
        JobsDbContext context,
        IReadOnlyList<(JobRunEvent Recorded, Func<CancellationToken, Task<int>> Change)> changes,
        CancellationToken cancellationToken) =>
        context.Database.CreateExecutionStrategy().ExecuteAsync(
            async token =>
            {
                context.ChangeTracker.Clear();
                await using var transaction = await context.Database.BeginTransactionAsync(token).ConfigureAwait(false);
                for (var index = 0; index < changes.Count; index++)
                {
                    if (await changes[index].Change(token).ConfigureAwait(false) > 0)
                    {
                        context.Events.Add(changes[index].Recorded);
                        await context.SaveChangesAsync(token).ConfigureAwait(false);
                        await transaction.CommitAsync(token).ConfigureAwait(false);

                        return index;
                    }
                }

                for (var index = 0; index < changes.Count; index++)
                {
                    var id = changes[index].Recorded.Id;
                    if (await context.Events.AnyAsync(e => e.Id == id, token).ConfigureAwait(false))
                    {
                        return index;
                    }
                }

                return -1;
            },
            cancellationToken);
}
