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

    public static readonly Error VersionInvalid = Field("user.version-invalid", VersionMember, "The version is not one the API issued for a person.");

    public static readonly Error RoleNotAssignable = Field("user.role-not-assignable", nameof(User.Roles), "Only the Finance, HR and Manager roles are assigned in the ERP; Administrator and Employee come from the app roles of the person's Entra account.");

    public static readonly Error NotFound = Error.NotFound("user.not-found", "No person has this id.");

    public static readonly Error OwnRecordNotFound = Error.NotFound("user.not-found", "The ERP holds no record of the signed-in person.");

    public static readonly Error EntraObjectIdTaken = Error.Conflict("user.entra-object-id-taken", "Another person's record already has this Entra object id.");

    public static readonly Error WorkEmailTaken = Error.Conflict("user.work-email-taken", "Another person's record already has this work email.");

    public static readonly Error EmployeeCodeTaken = Error.Conflict("user.employee-code-taken", "Another person's record already has this employee code.");

    public static readonly Error LinkedToAnotherAccount = Error.Conflict("user.linked-to-another-account", "The record belongs to another Entra account.");

    public static readonly Error OwnStatus = Error.Failure("user.own-status", "An administrator cannot change the status of their own record.");

    public static readonly Error OwnDeletion = Error.Failure("user.own-deletion", "An administrator cannot delete their own record.");

    public static readonly Error ManagesPeople = Error.Conflict("user.manages-people", "People report to the person today or from a later date; record another manager for them first.");

    public static readonly Error ActorInactive = Error.Conflict("user.actor-inactive", "The record of the person making the change is deactivated or deleted, so the change was not saved.");

    public static readonly Error TeamHandoverRequired = Error.Failure("user.team-handover-required", "People report to the person today or from a later date; name who they report to from today, another active person or no one, to deactivate the person.");

    public static readonly Error TeamManagerSamePerson = Error.Failure("user.team-manager-same-person", "The team cannot report to the person being deactivated.");

    public static readonly Error TeamManagerNotFound = Error.Failure("user.team-manager-not-found", "No person has the id given as the team's new manager.");

    public static readonly Error TeamManagerInactive = Error.Failure("user.team-manager-inactive", "The team's new manager is deactivated, so they cannot be anyone's manager today or later.");

    public static readonly Error TeamManagerInTeam = Error.Failure("user.team-manager-in-team", "The team's new manager reports to the person being deactivated, today or from a later date, so they cannot take the team.");

    public static readonly Error TeamReportingCycle = Error.Failure("user.team-reporting-cycle", "The team's new manager reports, through their own managers, to someone in the team, who would then report to themself.");

    private static Error Field(string code, string member, string message) =>
        Error.Validation(code, message, new Dictionary<string, string[]>(StringComparer.Ordinal) { [member] = [message] });
}
