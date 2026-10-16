using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Origins;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes.Capture;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing;

// One reading of the clock serves the stamps and the change records of a save, so a record's time is exactly its row's stamp.
// A save refused for concurrency never reports SaveChangesFailed, so its records are forgotten when the refusal is raised.
public sealed class AuditingSaveChangesInterceptor(IActorContext actor, IRequestOrigin origin, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        throw new NotSupportedException("SaveChanges is not supported; call SaveChangesAsync so every database write stays asynchronous.");

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context)
        {
            var now = timeProvider.GetUtcNow();
            var actorId = actor.ActorId;
            ChangeCapture.Forget(context);
            ChangeCapture.RefuseRewrites(context.ChangeTracker);
            Stamp(context.ChangeTracker, now, actorId);
            ChangeCapture.Record(context, new ChangeStamp(now, actorId, origin.ClientAddress, origin.CorrelationId));
        }

        return ValueTask.FromResult(result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        ChangeCapture.Forget(eventData.Context);

        return ValueTask.FromResult(result);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        ChangeCapture.Forget(eventData.Context);

        return Task.CompletedTask;
    }

    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        ChangeCapture.Forget(eventData.Context);

        return Task.CompletedTask;
    }

    public override ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(ConcurrencyExceptionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (!result.IsSuppressed)
        {
            ChangeCapture.Forget(eventData.Context);
        }

        return ValueTask.FromResult(result);
    }

    public static void Stamp(ChangeTracker changeTracker, DateTimeOffset now, Guid actorId)
    {
        ArgumentNullException.ThrowIfNull(changeTracker);

        foreach (var entry in changeTracker.Entries().ToList())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity is IAuditable)
                    {
                        entry.Property(nameof(IAuditable.CreatedAt)).CurrentValue = now;
                        entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = actorId;
                        entry.Property(nameof(IAuditable.ModifiedAt)).CurrentValue = null;
                        entry.Property(nameof(IAuditable.ModifiedBy)).CurrentValue = null;
                    }

                    if (entry.Entity is ISoftDeletable)
                    {
                        entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = false;
                        entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = null;
                        entry.Property(nameof(ISoftDeletable.DeletedBy)).CurrentValue = null;
                    }

                    break;
                case EntityState.Modified:
                    if (entry.Entity is IAuditable)
                    {
                        StampModified(entry, now, actorId);
                    }

                    if (entry.Entity is ISoftDeletable)
                    {
                        StampDeletion(entry, now, actorId);
                    }

                    break;
                case EntityState.Deleted when entry.Entity is ISoftDeletable:
                    if (entry.Property(nameof(ISoftDeletable.IsDeleted)).OriginalValue is true)
                    {
                        entry.State = EntityState.Unchanged;
                        break;
                    }

                    entry.State = EntityState.Modified;
                    entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
                    StampDeletion(entry, now, actorId);
                    if (entry.Entity is IAuditable)
                    {
                        StampModified(entry, now, actorId);
                    }

                    break;
            }
        }
    }

    private static void StampModified(EntityEntry entry, DateTimeOffset now, Guid actorId)
    {
        entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
        entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
        entry.Property(nameof(IAuditable.ModifiedAt)).CurrentValue = now;
        entry.Property(nameof(IAuditable.ModifiedBy)).CurrentValue = actorId;
    }

    private static void StampDeletion(EntityEntry entry, DateTimeOffset now, Guid actorId)
    {
        var isDeleted = entry.Property(nameof(ISoftDeletable.IsDeleted));
        var deletedAt = entry.Property(nameof(ISoftDeletable.DeletedAt));
        var deletedBy = entry.Property(nameof(ISoftDeletable.DeletedBy));
        switch (wasDeleted: isDeleted.OriginalValue is true, deleted: isDeleted.CurrentValue is true)
        {
            case (false, true):
                deletedAt.CurrentValue = now;
                deletedBy.CurrentValue = actorId;
                break;
            case (true, false):
                deletedAt.CurrentValue = null;
                deletedBy.CurrentValue = null;
                break;
            default:
                isDeleted.IsModified = false;
                deletedAt.IsModified = false;
                deletedBy.IsModified = false;
                break;
        }
    }
}
