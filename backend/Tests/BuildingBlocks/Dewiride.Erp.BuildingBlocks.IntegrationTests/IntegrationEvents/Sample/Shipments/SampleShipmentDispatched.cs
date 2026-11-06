using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Shipments;

public sealed record SampleShipmentDispatched(SampleShipmentId ShipmentId, int Parcels, DateTimeOffset OccurredOn) : IDomainEvent;
