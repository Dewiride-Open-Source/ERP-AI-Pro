using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Dewiride.Erp.Modules.Identity.Users.Shared;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Application;

internal static class DepartmentLimits
{
    public const int NameMaxLength = Department.NameMaxLength;

    public const int VersionMaxLength = RecordVersion.InputMaxLength;
}
