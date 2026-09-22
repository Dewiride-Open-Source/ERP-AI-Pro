using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

public readonly record struct SampleLineId(Guid Value) : IStronglyTypedId<SampleLineId>
{
    public static SampleLineId Create() => new(Guid.CreateVersion7());

    public static SampleLineId From(Guid value) => new(value);
}
