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
    string? Version,
    [property: Description("Who the person's team reports to from today, given with a deactivation only: the team is everyone whose position names the person as manager today or from a later date, and deactivating a person with a team needs it. Giving it also needs the permissions identity.users.positions.record and identity.users.positions.withdraw.")]
    TeamHandoverRequest? Team = null) : IValidatableObject
{
    public const string TeamWithActiveMessage = "A team is handed over only when the person is deactivated.";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Team is not null && Status == PersonStatus.Active)
        {
            yield return new ValidationResult(TeamWithActiveMessage, [nameof(Team)]);
        }
    }
}
