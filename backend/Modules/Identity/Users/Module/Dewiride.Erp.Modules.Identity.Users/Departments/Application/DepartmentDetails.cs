using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;
using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Application;

internal sealed record DepartmentDetails(Guid Id, string Name, Guid? ParentId, string Version)
{
    public static DepartmentDetails From(Department department)
    {
        ArgumentNullException.ThrowIfNull(department);

        return new(department.Id.Value, department.Name, department.ParentId?.Value, RecordVersion.Encode(department.RowVersion));
    }
}
