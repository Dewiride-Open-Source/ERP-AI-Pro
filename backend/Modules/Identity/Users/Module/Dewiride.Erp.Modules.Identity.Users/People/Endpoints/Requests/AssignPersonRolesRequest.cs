using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.People.Application;

namespace Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Requests;

public sealed record AssignPersonRolesRequest(
    [property: Description("Roles the person holds in the ERP from now on, in place of those they held; their app roles in Microsoft Entra give them Administrator and Employee besides.")]
    [property: Required]
    [property: MaxLength(PersonLimits.RolesMaxCount)]
    IReadOnlyList<PersonRole>? Roles,
    [property: Description("Version of the record the change was made from, as the API last returned it.")]
    [property: Required]
    [property: StringLength(PersonLimits.VersionMaxLength)]
    string? Version);
