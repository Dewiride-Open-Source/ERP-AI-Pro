using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Identity.Users.People.Domain;

internal static class UserErrors
{
    public const string VersionMember = "Version";

    public static readonly Error DisplayNameRequired = Field("user.display-name-required", nameof(User.DisplayName), "A person's display name must not be empty.");

    public static readonly Error DisplayNameTooLong = Field("user.display-name-too-long", nameof(User.DisplayName), $"A display name has at most {User.DisplayNameMaxLength} characters.");

    public static readonly Error WorkEmailInvalid = Field("user.work-email-invalid", nameof(User.WorkEmail), $"The work email must be an email address of at most {User.WorkEmailMaxLength} characters.");

    public static readonly Error EmployeeCodeInvalid = Field("user.employee-code-invalid", nameof(User.EmployeeCode), $"An employee code has at most {User.EmployeeCodeMaxLength} letters, digits, hyphens, slashes and underscores, and starts with a letter or a digit.");

    public static readonly Error PhoneNumberInvalid = Field("user.phone-number-invalid", nameof(User.PhoneNumber), "The phone number must be an international number starting with + and its country code, or a 10-digit Indian mobile number.");

    public static readonly Error DesignationTooLong = Field("user.designation-too-long", nameof(User.Designation), $"A designation has at most {User.DesignationMaxLength} characters.");

    public static readonly Error VersionInvalid = Field("user.version-invalid", VersionMember, "The version is not one the API issued for a person.");

    public static readonly Error NotFound = Error.NotFound("user.not-found", "No person has this id.");

    public static readonly Error EntraObjectIdTaken = Error.Conflict("user.entra-object-id-taken", "Another person's record already has this Entra object id.");

    public static readonly Error WorkEmailTaken = Error.Conflict("user.work-email-taken", "Another person's record already has this work email.");

    public static readonly Error EmployeeCodeTaken = Error.Conflict("user.employee-code-taken", "Another person's record already has this employee code.");

    public static readonly Error LinkedToAnotherAccount = Error.Conflict("user.linked-to-another-account", "The record belongs to another Entra account.");

    public static readonly Error OwnStatus = Error.Failure("user.own-status", "An administrator cannot change the status of their own record.");

    public static readonly Error OwnDeletion = Error.Failure("user.own-deletion", "An administrator cannot delete their own record.");

    private static Error Field(string code, string member, string message) =>
        Error.Validation(code, message, new Dictionary<string, string[]>(StringComparer.Ordinal) { [member] = [message] });
}
