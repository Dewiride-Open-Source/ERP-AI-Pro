using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;

// That a subscriber handled an event, written in the subscriber's own transaction, so a delivery that runs again finds it and
// changes nothing. Its key is the event and the subscriber's job name.
[EntityTypeConfiguration(typeof(ProcessedMessageConfiguration))]
public sealed class ProcessedMessage : IAppendOnly
{
    internal ProcessedMessage(IntegrationEventId eventId, string subscriber, DateTimeOffset processedAt)
    {
        EventId = eventId;
        Subscriber = subscriber;
        ProcessedAt = processedAt;
    }

    private ProcessedMessage()
    {
    }

    public IntegrationEventId EventId { get; private set; }

    public string Subscriber { get; private set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; private set; }
}
