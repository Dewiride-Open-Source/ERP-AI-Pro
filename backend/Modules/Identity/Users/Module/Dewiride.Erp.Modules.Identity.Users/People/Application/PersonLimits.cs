using Dewiride.Erp.Modules.Identity.Users.People.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application;

internal static class PersonLimits
{
    public const int DisplayNameMaxLength = User.DisplayNameMaxLength;

    public const int WorkEmailMaxLength = User.WorkEmailMaxLength;

    public const int EmployeeCodeMaxLength = User.EmployeeCodeMaxLength;

    public const int PhoneNumberInputMaxLength = 32;

    public const int DesignationMaxLength = User.DesignationMaxLength;

    public const int VersionMaxLength = 64;
}
