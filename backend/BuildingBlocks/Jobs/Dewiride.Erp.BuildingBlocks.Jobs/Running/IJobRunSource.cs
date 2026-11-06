namespace Dewiride.Erp.BuildingBlocks.Jobs.Running;

// Turns records a building block keeps into runs, as the outbox relay turns committed integration events into deliveries. The
// runner asks every source at each poll, after the scheduler and before it claims, so a run a source enqueues is claimed in the
// same poll, and a source that fails costs that poll its own work alone.
public interface IJobRunSource
{
    Task EnqueueDueAsync(CancellationToken cancellationToken);
}
