using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Definitions;

// A run is executed at least once: a process that stops after a job's work but before its outcome is recorded leaves the run to
// be retried, and an administrator's re-run can overlap a scheduled run, so a job is idempotent and safe to run twice at once.
// It runs as the system, never as whoever enqueued it, and reads the state it works on when it runs.
public interface IJob
{
    Task<Result> RunAsync(JobContext context, CancellationToken cancellationToken);
}

public interface IJob<in TPayload>
    where TPayload : class
{
    Task<Result> RunAsync(TPayload payload, JobContext context, CancellationToken cancellationToken);
}
