using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Shared.Versioning;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application;

internal static class PersonLimits
{
    public const int DisplayNameMaxLength = User.DisplayNameMaxLength;

    public const int WorkEmailMaxLength = User.WorkEmailMaxLength;

    public const int EmployeeCodeMaxLength = User.EmployeeCodeMaxLength;

    public const int PhoneNumberInputMaxLength = 32;

    public const int VersionMaxLength = RecordVersion.InputMaxLength;

    public const int RolesMaxCount = 3;
}
