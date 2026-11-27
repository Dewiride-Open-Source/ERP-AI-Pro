using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample;

public readonly record struct SampleClaimId(Guid Value) : IStronglyTypedId<SampleClaimId>
{
    public static SampleClaimId Create() => new(Guid.CreateVersion7());

    public static SampleClaimId From(Guid value) => new(value);
}
