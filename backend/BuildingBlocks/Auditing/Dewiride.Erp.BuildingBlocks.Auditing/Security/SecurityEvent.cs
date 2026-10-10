namespace Dewiride.Erp.BuildingBlocks.Auditing.Security;

internal sealed class SecurityEvent
{
    public const int DetailMaxLength = 128;

    public const int ClientAddressMaxLength = 64;

    public const int CorrelationIdMaxLength = 64;

    private SecurityEvent()
    {
    }

    public SecurityEventId Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public SecurityEventKind Kind { get; private set; }

    public string? Detail { get; private set; }

    public Guid? ActorObjectId { get; private set; }

    public Guid? ClientApplicationId { get; private set; }

    public string? ClientAddress { get; private set; }

    public string? CorrelationId { get; private set; }

    public static SecurityEvent Record(SecurityEventEntry entry, DateTimeOffset occurredAt)
    {
        if (!Enum.IsDefined(entry.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(entry), entry.Kind, "The kind of a security event must be one of SecurityEventKind.");
        }

        return new SecurityEvent
        {
            Id = SecurityEventId.Create(),
            OccurredAt = occurredAt.ToUniversalTime(),
            Kind = entry.Kind,
            Detail = Bounded(entry.Detail, DetailMaxLength, nameof(entry.Detail)),
            ActorObjectId = entry.ActorObjectId,
            ClientApplicationId = entry.ClientApplicationId,
            ClientAddress = Bounded(entry.ClientAddress, ClientAddressMaxLength, nameof(entry.ClientAddress)),
            CorrelationId = Bounded(entry.CorrelationId, CorrelationIdMaxLength, nameof(entry.CorrelationId)),
        };
    }

    private static string? Bounded(string? value, int maxLength, string name) =>
        value is { Length: var length } && length > maxLength
            ? throw new ArgumentOutOfRangeException(name, length, $"A security event's {name} is at most {maxLength} characters.")
            : value;
}
