using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Receipts;

public readonly record struct SampleReceiptId(Guid Value) : IStronglyTypedId<SampleReceiptId>
{
    public static SampleReceiptId Create() => new(Guid.CreateVersion7());

    public static SampleReceiptId From(Guid value) => new(value);
}
