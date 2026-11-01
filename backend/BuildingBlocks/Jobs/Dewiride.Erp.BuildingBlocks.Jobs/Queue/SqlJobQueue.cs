using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Dewiride.Erp.BuildingBlocks.Jobs.Telemetry;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Queue;

// The run and its Enqueued event are inserted by one save, which the unique key of job and idempotency key turns into a lookup
// when the key was used: a retry of that save after a commit whose acknowledgement was lost finds its own run the same way. The
// entities leave the context once the save has run, written or not, so a failed enqueue never rides along with the next save of
// the scope.
internal sealed class SqlJobQueue(
    JobsDbContext context,
    JobCatalog catalog,
    JobSignal signal,
    JobsMetrics metrics,
    IActorContext actor,
    TimeProvider timeProvider) : IJobQueue
{
    private const int UniqueIndexViolation = 2601;

    private const int UniqueConstraintViolation = 2627;

    private const int MaxAttempts = 3;

    public Task<JobRunId> EnqueueAsync<TJob>(string idempotencyKey, CancellationToken cancellationToken)
        where TJob : class, IJob
    {
        var registration = catalog.Require(typeof(TJob), payloadType: null);
        JobKeys.EnsureEnqueueable(idempotencyKey);

        return EnqueueAsync(registration, idempotencyKey, payload: null, cancellationToken);
    }

    public Task<JobRunId> EnqueueAsync<TJob, TPayload>(TPayload payload, string idempotencyKey, CancellationToken cancellationToken)
        where TJob : class, IJob<TPayload>
        where TPayload : class
    {
        var registration = catalog.Require(typeof(TJob), typeof(TPayload));
        JobKeys.EnsureEnqueueable(idempotencyKey);

        return EnqueueAsync(registration, idempotencyKey, JobPayloads.Serialize(payload), cancellationToken);
    }

    private async Task<JobRunId> EnqueueAsync(JobRegistration registration, string idempotencyKey, string? payload, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var now = timeProvider.GetUtcNow();
            var run = JobRun.Enqueue(JobRunId.Create(), registration.Name, idempotencyKey, payload, now, now, actor.ActorId);
            var enqueued = JobRunEvent.Record(run.Id, JobRunEventKind.Enqueued, now, run.RequestedBy);
            context.Runs.Add(run);
            context.Events.Add(enqueued);
            bool created;
            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                created = true;
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
            {
                created = false;
            }
            finally
            {
                context.Entry(run).State = EntityState.Detached;
                context.Entry(enqueued).State = EntityState.Detached;
            }

            if (created)
            {
                metrics.Enqueued(registration.Name, created: true);
                signal.Wake();
                return run.Id;
            }

            var existing = await context.Runs
                .AsNoTracking()
                .Where(r => r.JobName == registration.Name && r.IdempotencyKey == idempotencyKey)
                .Select(r => new { r.Id, r.Payload })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                continue;
            }

            if (!JobPayloads.SameAs(existing.Payload, payload, registration.PayloadType))
            {
                throw new InvalidOperationException($"The job {registration.Name} already has a run with this idempotency key and a different payload.");
            }

            metrics.Enqueued(registration.Name, created: false);
            return existing.Id;
        }

        throw new InvalidOperationException($"A run of {registration.Name} could not be enqueued after {MaxAttempts} attempts; its key kept being taken and released.");
    }
}
