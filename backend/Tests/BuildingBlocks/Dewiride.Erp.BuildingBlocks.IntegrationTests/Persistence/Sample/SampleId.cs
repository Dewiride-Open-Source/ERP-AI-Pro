using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

public readonly record struct SampleId(Guid Value) : IStronglyTypedId<SampleId>
{
    public static SampleId Create() => new(Guid.CreateVersion7());

    public static SampleId From(Guid value) => new(value);
}
