using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Domain;

internal readonly record struct DesignationId(Guid Value) : IStronglyTypedId<DesignationId>
{
    public static DesignationId Create() => new(Guid.CreateVersion7());

    public static DesignationId From(Guid value) => new(value);
}
