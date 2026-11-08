using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Numbering.Sample;

public readonly record struct SampleInvoiceId(Guid Value) : IStronglyTypedId<SampleInvoiceId>
{
    public static SampleInvoiceId Create() => new(Guid.CreateVersion7());

    public static SampleInvoiceId From(Guid value) => new(value);
}
