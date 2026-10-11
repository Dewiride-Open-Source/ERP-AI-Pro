using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Dewiride.Erp.Modules.Identity.Users.Shared.Versioning;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Application;

internal sealed record DepartmentDetails(Guid Id, string Name, Guid? ParentId, string Version)
{
    public static DepartmentDetails From(Department department)
    {
        ArgumentNullException.ThrowIfNull(department);

        return new(department.Id.Value, department.Name, department.ParentId?.Value, RecordVersion.Encode(department.RowVersion));
    }
}
