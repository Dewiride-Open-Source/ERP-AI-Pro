namespace Dewiride.Erp.BuildingBlocks.Idempotency.Storage;

internal sealed record BeginOutcome(BeginState State, IdempotencyRecord? Existing)
{
    public static readonly BeginOutcome Started = new(BeginState.Started, null);
}
