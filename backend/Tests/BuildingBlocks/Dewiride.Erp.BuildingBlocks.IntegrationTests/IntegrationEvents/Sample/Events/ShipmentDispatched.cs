using Dewiride.Erp.BuildingBlocks.Kernel.Events;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Events;

public sealed record ShipmentDispatched(Guid ShipmentId, int Parcels, DateTimeOffset DispatchedAt, DateOnly? DeliveryDue = null) : IIntegrationEvent
{
    public static string Name => "test.events.shipment-dispatched";
}
