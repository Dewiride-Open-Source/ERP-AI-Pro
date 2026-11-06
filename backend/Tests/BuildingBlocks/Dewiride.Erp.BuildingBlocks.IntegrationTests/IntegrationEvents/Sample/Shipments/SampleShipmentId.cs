using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Shipments;

public readonly record struct SampleShipmentId(Guid Value) : IStronglyTypedId<SampleShipmentId>
{
    public static SampleShipmentId Create() => new(Guid.CreateVersion7());

    public static SampleShipmentId From(Guid value) => new(value);
}
