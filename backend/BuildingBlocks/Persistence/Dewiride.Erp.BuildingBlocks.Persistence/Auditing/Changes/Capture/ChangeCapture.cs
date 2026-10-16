using System.Runtime.CompilerServices;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes.Capture;

// The capture runs in SavingChangesAsync after the stamps, so a soft delete arrives as an update that sets IsDeleted and a
// restore as one that clears it. Its records ride in the same save as the changes: the execution strategy resends them with
// the changes when it retries the save, and the unit of work, which clears the change tracker before it runs a command again,
// has them captured afresh. A save that does not complete leaves them tracked, so the records the capture added to a context
// are forgotten when its save ends and again before its next save captures anything.
internal static class ChangeCapture
{
    private static readonly ConditionalWeakTable<DbContext, List<ChangeRecord>> Captured = new();

    public static void Forget(DbContext? context)
    {
        if (context is null || !Captured.TryGetValue(context, out var records) || records.Count == 0)
        {
            return;
        }

        // A save refused for concurrency forgets its records while it still runs, when detecting changes would read state the
        // save has not finished with; a record never changes once captured, so nothing needs detecting.
        var changeTracker = context.ChangeTracker;
        var detectsChanges = changeTracker.AutoDetectChangesEnabled;
        changeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var record in records)
            {
                context.Entry(record).State = EntityState.Detached;
            }

            records.Clear();
        }
        finally
        {
            changeTracker.AutoDetectChangesEnabled = detectsChanges;
        }
    }

    public static void RefuseRewrites(ChangeTracker changeTracker)
    {
        ArgumentNullException.ThrowIfNull(changeTracker);

        var rewrite = changeTracker.Entries<IAppendOnly>().FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (rewrite is not null)
        {
            throw new InvalidOperationException($"{rewrite.Metadata.DisplayName()} is append-only: a saved row is never updated or deleted through the change tracker, and only the retention of its table deletes rows.");
        }
    }

    public static void Record(DbContext context, ChangeStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<ChangeRecord>? records = null;
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && RecordedEntityTypes.IsRecorded(entry.Metadata)
                && Capture(entry, RecordedEntityType.Of(entry.Metadata), stamp) is { } record)
            {
                (records ??= []).Add(record);
            }
        }

        if (records is null)
        {
            return;
        }

        if (context.Model.FindEntityType(typeof(ChangeRecord)) is null)
        {
            throw new InvalidOperationException($"{context.GetType().Name} saves changes of recorded types but maps no change records; derive it from {nameof(ModuleDbContext)}, which maps them.");
        }

        context.AddRange(records);
        Captured.GetOrCreateValue(context).AddRange(records);
    }

    private static ChangeRecord? Capture(EntityEntry entry, RecordedEntityType type, ChangeStamp stamp)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                return Create(entry, type, stamp, ChangeOperation.Created, [.. type.Properties.Select(property => ChangedValue.Added(property, entry.Property(property.Property).CurrentValue))], original: false);
            case EntityState.Deleted:
                return Create(entry, type, stamp, ChangeOperation.Deleted, [.. type.Properties.Select(property => ChangedValue.Removed(property, entry.Property(property.Property).OriginalValue))], original: true);
            default:
                var operation = OperationOf(entry, type);
                var changed = Changed(entry, type);

                return operation == ChangeOperation.Updated && changed.Count == 0 ? null : Create(entry, type, stamp, operation, changed, original: false);
        }
    }

    private static ChangeOperation OperationOf(EntityEntry entry, RecordedEntityType type)
    {
        if (!type.IsSoftDeletable)
        {
            return ChangeOperation.Updated;
        }

        var isDeleted = entry.Property(nameof(ISoftDeletable.IsDeleted));

        return (wasDeleted: isDeleted.OriginalValue is true, deleted: isDeleted.CurrentValue is true) switch
        {
            (false, true) => ChangeOperation.Deleted,
            (true, false) => ChangeOperation.Restored,
            _ => ChangeOperation.Updated,
        };
    }

    private static List<ChangedValue> Changed(EntityEntry entry, RecordedEntityType type)
    {
        var changed = new List<ChangedValue>();
        foreach (var property in type.Properties)
        {
            var value = entry.Property(property.Property);
            if (value.IsModified && !property.Property.GetValueComparer().Equals(value.OriginalValue, value.CurrentValue))
            {
                changed.Add(ChangedValue.Changed(property, value.OriginalValue, value.CurrentValue));
            }
        }

        return changed;
    }

    private static ChangeRecord Create(EntityEntry entry, RecordedEntityType type, ChangeStamp stamp, ChangeOperation operation, List<ChangedValue> values, bool original)
    {
        var entityId = IdOf(entry, type.KeyProperty, original);
        var subjectId = type.SubjectProperty is { } subject ? IdOf(entry, subject, original) : entityId;

        return ChangeRecord.Record(stamp, operation, type.Key, entityId, type.SubjectKey, subjectId, ChangeValueWriter.Write(values), checked((short)values.Count));
    }

    private static Guid IdOf(EntityEntry entry, IProperty property, bool original)
    {
        var value = entry.Property(property);
        if (value.IsTemporary)
        {
            throw new InvalidOperationException($"{entry.Metadata.DisplayName()}.{property.Name} has no value until the save, so the change cannot name its row; set it in code.");
        }

        var held = original ? value.OriginalValue : value.CurrentValue;
        var provided = held is not null && property.GetValueConverter() is { } converter ? converter.ConvertToProvider(held) : held;

        return provided is Guid id
            ? id
            : throw new InvalidOperationException($"{entry.Metadata.DisplayName()}.{property.Name} holds no Guid, so the change cannot name its row.");
    }
}
