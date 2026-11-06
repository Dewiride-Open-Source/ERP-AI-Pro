using System.Collections.Concurrent;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Shipments;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Integration.Publishers;

// The event id each dispatch published, so a test finds its own event among those every other test left in the shared outbox.
public sealed class PublishedEvents
{
    private readonly ConcurrentDictionary<SampleShipmentId, IntegrationEventId> _byShipment = new();

    public void Record(SampleShipmentId shipmentId, IntegrationEventId eventId) => _byShipment[shipmentId] = eventId;

    public IntegrationEventId Of(SampleShipmentId shipmentId) =>
        _byShipment.TryGetValue(shipmentId, out var eventId) ? eventId : throw new InvalidOperationException($"No event was published for the shipment {shipmentId.Value}.");

    public bool Contains(SampleShipmentId shipmentId) => _byShipment.ContainsKey(shipmentId);
}
