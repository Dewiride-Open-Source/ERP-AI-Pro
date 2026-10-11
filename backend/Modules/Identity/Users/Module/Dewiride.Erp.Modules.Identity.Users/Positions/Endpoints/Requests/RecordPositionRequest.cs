using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Requests;

public sealed record RecordPositionRequest(
    [property: Description("Date the position starts, in the past or the future; it holds until the date the person's next position starts.")]
    [property: Required]
    DateOnly? EffectiveFrom,
    [property: Description("Department the person is in from that date; absent puts them in none.")]
    Guid? DepartmentId,
    [property: Description("Designation the person holds from that date; absent gives them none.")]
    Guid? DesignationId,
    [property: Description("Person they report to from that date, never themself; absent gives them no manager.")]
    Guid? ManagerId);
