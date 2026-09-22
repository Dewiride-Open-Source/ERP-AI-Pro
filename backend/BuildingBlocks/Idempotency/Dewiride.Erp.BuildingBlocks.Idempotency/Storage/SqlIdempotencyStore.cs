using Dewiride.Erp.BuildingBlocks.Idempotency.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Idempotency.Storage;

internal sealed class SqlIdempotencyStore(IdempotencyDbContext context) : IIdempotencyStore
{
    private const int UniqueIndexViolation = 2601;

    private const int UniqueConstraintViolation = 2627;

    private const int MaxAttempts = 3;

    public async Task<BeginOutcome> BeginAsync(Guid actorId, Guid key, byte[] fingerprint, DateTimeOffset now, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (await TryInsertAsync(actorId, key, fingerprint, now, expiresAt, cancellationToken).ConfigureAwait(false))
            {
                return BeginOutcome.Started;
            }

            var existing = await context.Records.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == actorId && r.Key == key, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                continue;
            }

            if (existing.ExpiresAt <= now && attempt == 0)
            {
                await context.Records.Where(r => r.ActorId == actorId && r.Key == key && r.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!existing.Matches(fingerprint))
            {
                return new BeginOutcome(BeginState.FingerprintMismatch, existing);
            }

            return new BeginOutcome(existing.Status == IdempotencyStatus.Completed ? BeginState.Completed : BeginState.InProgress, existing);
        }

        throw new InvalidOperationException($"Idempotency key {key} for actor {actorId} could not be claimed after {MaxAttempts} attempts.");
    }

    public Task CompleteAsync(Guid actorId, Guid key, StoredResponse response, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        return context.Records
            .Where(r => r.ActorId == actorId && r.Key == key && r.Status == IdempotencyStatus.InProgress)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.Status, IdempotencyStatus.Completed)
                    .SetProperty(r => r.StatusCode, response.StatusCode)
                    .SetProperty(r => r.ContentType, response.ContentType)
                    .SetProperty(r => r.Location, response.Location)
                    .SetProperty(r => r.Body, response.Body)
                    .SetProperty(r => r.CompletedAt, now),
                cancellationToken);
    }

    public Task AbandonAsync(Guid actorId, Guid key, CancellationToken cancellationToken) =>
        context.Records
            .Where(r => r.ActorId == actorId && r.Key == key && r.Status == IdempotencyStatus.InProgress)
            .ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        context.Records.Where(r => r.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);

    private async Task<bool> TryInsertAsync(Guid actorId, Guid key, byte[] fingerprint, DateTimeOffset now, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var record = IdempotencyRecord.Start(actorId, key, fingerprint, now, expiresAt);
        context.Records.Add(record);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            context.Entry(record).State = EntityState.Detached;
            return false;
        }
    }
}
