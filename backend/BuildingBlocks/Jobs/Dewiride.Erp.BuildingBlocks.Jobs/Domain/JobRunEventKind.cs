namespace Dewiride.Erp.BuildingBlocks.Jobs.Domain;

public enum JobRunEventKind
{
    Enqueued = 1,
    Started = 2,
    Succeeded = 3,
    Failed = 4,
    DeadLettered = 5,
    Interrupted = 6,
    LeaseExpired = 7,
    CancelRequested = 8,
    Cancelled = 9,
    Rerun = 10,
}
