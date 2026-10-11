using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Domain;

internal readonly record struct PositionId(Guid Value) : IStronglyTypedId<PositionId>
{
    public static PositionId Create() => new(Guid.CreateVersion7());

    public static PositionId From(Guid value) => new(value);
}
