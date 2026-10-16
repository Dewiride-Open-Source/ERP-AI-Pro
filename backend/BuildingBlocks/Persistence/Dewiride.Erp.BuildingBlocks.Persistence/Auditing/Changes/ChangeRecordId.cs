using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

public readonly record struct ChangeRecordId(Guid Value) : IStronglyTypedId<ChangeRecordId>
{
    public static ChangeRecordId Create() => new(Guid.CreateVersion7());

    public static ChangeRecordId From(Guid value) => new(value);
}
