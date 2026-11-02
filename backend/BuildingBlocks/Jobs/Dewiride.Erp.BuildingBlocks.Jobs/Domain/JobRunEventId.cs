using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Domain;

internal readonly record struct JobRunEventId(Guid Value) : IStronglyTypedId<JobRunEventId>
{
    public static JobRunEventId Create() => new(Guid.CreateVersion7());

    public static JobRunEventId From(Guid value) => new(value);
}
