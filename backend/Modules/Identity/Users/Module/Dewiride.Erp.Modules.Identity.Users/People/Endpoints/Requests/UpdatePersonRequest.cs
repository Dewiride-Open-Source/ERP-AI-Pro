using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.Modules.Identity.Users.People.Application;

namespace Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Requests;

public sealed record UpdatePersonRequest(
    [property: Description("Employee code: letters, digits, hyphens, slashes and underscores, starting with a letter or a digit, unique among the people of the ERP; empty clears it.")]
    [property: StringLength(PersonLimits.EmployeeCodeMaxLength)]
    string? EmployeeCode,
    [property: Description("Phone number: an international number starting with + and its country code, or a 10-digit Indian mobile number; empty clears it.")]
    [property: StringLength(PersonLimits.PhoneNumberInputMaxLength)]
    string? PhoneNumber,
    [property: Description("Designation, as free text; empty clears it.")]
    [property: StringLength(PersonLimits.DesignationMaxLength)]
    string? Designation,
    [property: Description("Date the person joined the company; absent clears it.")]
    DateOnly? DateOfJoining,
    [property: Description("Version of the record the edit was made from, as the API last returned it.")]
    [property: Required]
    [property: StringLength(PersonLimits.VersionMaxLength)]
    string? Version);
