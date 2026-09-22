namespace Dewiride.Erp.BuildingBlocks.Idempotency.Storage;

internal interface IIdempotencyStore
{
    Task<BeginOutcome> BeginAsync(Guid actorId, Guid key, byte[] fingerprint, DateTimeOffset now, DateTimeOffset expiresAt, CancellationToken cancellationToken);

    Task CompleteAsync(Guid actorId, Guid key, StoredResponse response, DateTimeOffset now, CancellationToken cancellationToken);

    Task AbandonAsync(Guid actorId, Guid key, CancellationToken cancellationToken);

    Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
