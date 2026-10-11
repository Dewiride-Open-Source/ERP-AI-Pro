using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Domain;

internal readonly record struct DepartmentId(Guid Value) : IStronglyTypedId<DepartmentId>
{
    public static DepartmentId Create() => new(Guid.CreateVersion7());

    public static DepartmentId From(Guid value) => new(value);
}
