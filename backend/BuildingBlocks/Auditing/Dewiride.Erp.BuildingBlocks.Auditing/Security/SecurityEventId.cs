using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Security;

internal readonly record struct SecurityEventId(Guid Value) : IStronglyTypedId<SecurityEventId>
{
    public static SecurityEventId Create() => new(Guid.CreateVersion7());

    public static SecurityEventId From(Guid value) => new(value);
}
