using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Receipts;

public sealed class SampleReceipt : AggregateRoot<SampleReceiptId>, IAuditable
{
    public const int RecordedByMaxLength = 128;

    private SampleReceipt(SampleReceiptId id)
        : base(id)
    {
    }

    public Guid ShipmentId { get; private set; }

    public Guid EventId { get; private set; }

    public int Parcels { get; private set; }

    public string RecordedBy { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public static SampleReceipt Record(Guid shipmentId, Guid eventId, int parcels, string recordedBy) =>
        new(SampleReceiptId.Create()) { ShipmentId = shipmentId, EventId = eventId, Parcels = parcels, RecordedBy = recordedBy };
}
