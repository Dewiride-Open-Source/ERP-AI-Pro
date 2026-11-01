using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Domain;

public readonly record struct JobRunId(Guid Value) : IStronglyTypedId<JobRunId>
{
    public static JobRunId Create() => new(Guid.CreateVersion7());

    public static JobRunId From(Guid value) => new(value);
}
