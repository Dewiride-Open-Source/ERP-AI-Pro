using System.ComponentModel;

namespace Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Requests;

public sealed record TeamHandoverRequest(
    [property: Description("Person the team reports to from today: an active person outside the team, never the person deactivated; null makes the team report to no one.")]
    Guid? ManagerId);
