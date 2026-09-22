namespace Dewiride.Erp.BuildingBlocks.Idempotency.Storage;

internal enum BeginState
{
    Started,
    InProgress,
    Completed,
    FingerprintMismatch,
}
