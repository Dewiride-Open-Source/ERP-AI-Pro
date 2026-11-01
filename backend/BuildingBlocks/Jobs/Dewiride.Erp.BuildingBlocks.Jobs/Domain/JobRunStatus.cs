namespace Dewiride.Erp.BuildingBlocks.Jobs.Domain;

public enum JobRunStatus
{
    Pending = 1,
    Running = 2,
    Failed = 3,
    Succeeded = 4,
    DeadLettered = 5,
    Cancelled = 6,
}
