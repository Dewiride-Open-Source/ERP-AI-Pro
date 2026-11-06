using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Publishing;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Shipments;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Integration.Publishers;

public sealed class ShipmentDispatchedPublisher(IIntegrationEventPublisher publisher, SampleDbContext context, PublishedEvents published) : IDomainEventHandler<SampleShipmentDispatched>
{
    public Task HandleAsync(SampleShipmentDispatched domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var eventId = publisher.Publish(context, new ShipmentDispatched(domainEvent.ShipmentId.Value, domainEvent.Parcels, domainEvent.OccurredOn));
        published.Record(domainEvent.ShipmentId, eventId);

        return Task.CompletedTask;
    }
}
