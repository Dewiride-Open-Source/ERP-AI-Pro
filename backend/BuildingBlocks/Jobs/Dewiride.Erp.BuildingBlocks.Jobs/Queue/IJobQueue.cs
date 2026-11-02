using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Queue;

// An enqueue with a key the job already used answers that run's id, whatever its state, while the run is kept, and a different
// payload under the same key is a programming error. The enqueue commits on a connection of its own, outside any unit of work, so
// a key built from the ids and dates of the change that asks for the run keeps a retried or rolled-back change from running the
// job twice. A payload holds identifiers only, never a secret or personal data, and is never shown, logged or returned.
public interface IJobQueue
{
    Task<JobRunId> EnqueueAsync<TJob>(string idempotencyKey, CancellationToken cancellationToken)
        where TJob : class, IJob;

    Task<JobRunId> EnqueueAsync<TJob, TPayload>(TPayload payload, string idempotencyKey, CancellationToken cancellationToken)
        where TJob : class, IJob<TPayload>
        where TPayload : class;
}
