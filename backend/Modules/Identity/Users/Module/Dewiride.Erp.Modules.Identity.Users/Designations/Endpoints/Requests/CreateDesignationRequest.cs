using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.Modules.Identity.Users.Designations.Application;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Endpoints.Requests;

public sealed record CreateDesignationRequest(
    [property: Description("Name of the designation, unique among the designations.")]
    [property: Required]
    [property: StringLength(DesignationLimits.NameMaxLength)]
    string? Name);
