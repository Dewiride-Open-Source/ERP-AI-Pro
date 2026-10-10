using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.Modules.Identity.Users.People.Application;

namespace Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Requests;

public sealed record InvitePersonRequest(
    [property: Description("Entra object id of the person in the company directory, as the directory search returned it.")]
    [property: Required]
    Guid? EntraObjectId,
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
    DateOnly? DateOfJoining) : IValidatableObject
{
    public const string EmptyObjectIdMessage = "The Entra object id must not be the empty GUID.";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EntraObjectId == Guid.Empty)
        {
            yield return new ValidationResult(EmptyObjectIdMessage, [nameof(EntraObjectId)]);
        }
    }
}
