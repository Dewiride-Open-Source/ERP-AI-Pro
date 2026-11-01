namespace Dewiride.Erp.BuildingBlocks.Jobs.Running.Attempts;

internal enum JobAttemptEnd
{
    Succeeded,
    Retryable,
    Permanent,
    Interrupted,
    Cancelled,
    LeaseLost,
}
