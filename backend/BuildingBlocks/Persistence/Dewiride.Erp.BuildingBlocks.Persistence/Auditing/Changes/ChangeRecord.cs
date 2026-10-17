using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

// The change capture alone records one, inside the save of the change it describes, so the change and its record commit or
// roll back together. EntityType and SubjectType hold change keys (RecordChangesAs), never table names, and Changes holds the
// value entries as a JSON array, or nothing when the record has none.
public sealed class ChangeRecord : IAppendOnly
{
    private ChangeRecord()
    {
    }

    public ChangeRecordId Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public ChangeOperation Operation { get; private set; }

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    public string SubjectType { get; private set; } = string.Empty;

    public Guid SubjectId { get; private set; }

    public Guid ActorId { get; private set; }

    public string? ClientAddress { get; private set; }

    public string? CorrelationId { get; private set; }

    public string? Changes { get; private set; }

    public short ValueCount { get; private set; }

    internal static ChangeRecord Record(
        ChangeStamp stamp,
        ChangeOperation operation,
        string entityType,
        Guid entityId,
        string subjectType,
        Guid subjectId,
        string? changes,
        short valueCount)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation), operation, "The operation of a change record must be one of ChangeOperation.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(valueCount);
        if (changes is null != (valueCount == 0))
        {
            throw new ArgumentException("A change record holds its value entries exactly when it counts some.", nameof(changes));
        }

        return new ChangeRecord
        {
            Id = ChangeRecordId.Create(),
            OccurredAt = stamp.OccurredAt.ToUniversalTime(),
            Operation = operation,
            EntityType = Key(entityType, nameof(entityType)),
            EntityId = entityId,
            SubjectType = Key(subjectType, nameof(subjectType)),
            SubjectId = subjectId,
            ActorId = stamp.ActorId,
            ClientAddress = Bounded(stamp.ClientAddress, ChangeRecordMapping.ClientAddressMaxLength, nameof(stamp.ClientAddress)),
            CorrelationId = Bounded(stamp.CorrelationId, ChangeRecordMapping.CorrelationIdMaxLength, nameof(stamp.CorrelationId)),
            Changes = changes,
            ValueCount = valueCount,
        };
    }

    private static string Key(string key, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, name);

        return Bounded(key, ChangeRecordMapping.KeyMaxLength, name)!;
    }

    private static string? Bounded(string? value, int maxLength, string name) =>
        value is { Length: var length } && length > maxLength
            ? throw new ArgumentOutOfRangeException(name, length, $"A change record's {name} is at most {maxLength} characters.")
            : value;
}
