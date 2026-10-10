using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.Modules.Identity.Users.People.Application;

namespace Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Requests;

public sealed record RegisterPersonRequest(
    [property: Description("Entra object id of the person's account, when it is known; without it the person's first sign-in links the record by its work email.")]
    Guid? EntraObjectId,
    [property: Description("Name the ERP shows for the person until their first sign-in brings the one Entra keeps.")]
    [property: Required]
    [property: StringLength(PersonLimits.DisplayNameMaxLength)]
    string? DisplayName,
    [property: Description("Work email of the person, which their first sign-in matches when the record names no Entra account.")]
    [property: Required]
    [property: StringLength(PersonLimits.WorkEmailMaxLength)]
    [property: EmailAddress]
    string? WorkEmail,
    [property: Description("Employee code: letters, digits, hyphens, slashes and underscores, starting with a letter or a digit, unique among the people of the ERP.")]
    [property: StringLength(PersonLimits.EmployeeCodeMaxLength)]
    string? EmployeeCode,
    [property: Description("Phone number: an international number starting with + and its country code, or a 10-digit Indian mobile number.")]
    [property: StringLength(PersonLimits.PhoneNumberInputMaxLength)]
    string? PhoneNumber,
    [property: Description("Designation, as free text.")]
    [property: StringLength(PersonLimits.DesignationMaxLength)]
    string? Designation,
    [property: Description("Date the person joined the company.")]
    DateOnly? DateOfJoining);
