using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.People.Application;

namespace Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Requests;

public sealed record ChangePersonStatusRequest(
    [property: Description("Status the record takes: a deactivated person cannot sign in, and their open sessions end at their next request.")]
    [property: EnumDataType(typeof(PersonStatus))]
    PersonStatus Status,
    [property: Description("Version of the record the change was made from, as the API last returned it.")]
    [property: Required]
    [property: StringLength(PersonLimits.VersionMaxLength)]
    string? Version);
