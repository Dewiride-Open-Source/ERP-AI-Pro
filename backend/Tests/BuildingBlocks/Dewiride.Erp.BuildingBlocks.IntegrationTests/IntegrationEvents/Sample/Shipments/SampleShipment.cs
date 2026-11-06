using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Shipments;

public sealed class SampleShipment : AggregateRoot<SampleShipmentId>, IAuditable
{
    private SampleShipment(SampleShipmentId id)
        : base(id)
    {
    }

    public int Parcels { get; private set; }

    public DateTimeOffset? DispatchedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public static SampleShipment Dispatch(SampleShipmentId id, int parcels, DateTimeOffset dispatchedAt)
    {
        var shipment = new SampleShipment(id) { Parcels = parcels, DispatchedAt = dispatchedAt };
        shipment.Raise(new SampleShipmentDispatched(id, parcels, dispatchedAt));

        return shipment;
    }
}
