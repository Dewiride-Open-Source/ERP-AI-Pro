using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.Modules.Identity.Users.Designations.Application;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Endpoints.Requests;

public sealed record UpdateDesignationRequest(
    [property: Description("Name of the designation, unique among the designations; every position that names it shows the new name.")]
    [property: Required]
    [property: StringLength(DesignationLimits.NameMaxLength)]
    string? Name,
    [property: Description("Version of the designation the change was made from, as the API last returned it.")]
    [property: Required]
    [property: StringLength(DesignationLimits.VersionMaxLength)]
    string? Version);
