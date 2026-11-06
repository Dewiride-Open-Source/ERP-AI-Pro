using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;

// An event of the module whose table holds it, written in the transaction of the change it describes and kept, once passed on,
// as the record that it was. The relay marks it passed on, so it is neither audited nor append-only.
[EntityTypeConfiguration(typeof(OutboxMessageConfiguration))]
public sealed class OutboxMessage
{
    public const int CorrelationIdMaxLength = 64;

    internal OutboxMessage(IntegrationEventId id, string eventName, string payload, DateTimeOffset occurredAt, string? correlationId, int depth)
    {
        if (correlationId is { Length: > CorrelationIdMaxLength })
        {
            throw new ArgumentException($"A correlation id is at most {CorrelationIdMaxLength} characters.", nameof(correlationId));
        }

        Id = id;
        EventName = eventName;
        Payload = payload;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
        Depth = depth;
    }

    private OutboxMessage()
    {
    }

    public IntegrationEventId Id { get; private set; }

    public string EventName { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public string? CorrelationId { get; private set; }

    public int Depth { get; private set; }

    public DateTimeOffset? RelayedAt { get; private set; }
}
