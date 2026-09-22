using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

public sealed class SampleLine : Entity<SampleLineId>
{
    public const int DescriptionMaxLength = 100;

    private SampleLine(SampleLineId id)
        : base(id)
    {
    }

    internal SampleLine(SampleLineId id, SampleId sampleId, string description)
        : base(id)
    {
        SampleId = sampleId;
        Description = description;
    }

    public SampleId SampleId { get; private set; }

    public string Description { get; private set; } = string.Empty;
}
