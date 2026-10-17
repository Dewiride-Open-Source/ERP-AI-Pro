using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample.Notes;

public readonly record struct SampleNoteId(Guid Value) : IStronglyTypedId<SampleNoteId>
{
    public static SampleNoteId Create() => new(Guid.CreateVersion7());

    public static SampleNoteId From(Guid value) => new(value);
}
