using System.Collections.Concurrent;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Shipments;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Integration.Publishers;

// Every event id each dispatch published, so a test finds its own event among those every other test left in the shared outbox
// and sees a dispatch that published more than one.
public sealed class PublishedEvents
{
    private readonly ConcurrentDictionary<SampleShipmentId, ConcurrentQueue<IntegrationEventId>> _byShipment = new();

    public void Record(SampleShipmentId shipmentId, IntegrationEventId eventId) => _byShipment.GetOrAdd(shipmentId, _ => new ConcurrentQueue<IntegrationEventId>()).Enqueue(eventId);

    public IReadOnlyList<IntegrationEventId> AllOf(SampleShipmentId shipmentId) => _byShipment.TryGetValue(shipmentId, out var eventIds) ? [.. eventIds] : [];

    public IntegrationEventId Of(SampleShipmentId shipmentId) =>
        AllOf(shipmentId) is [var eventId] ? eventId : throw new InvalidOperationException($"The shipment {shipmentId.Value} did not publish exactly one event.");
}
