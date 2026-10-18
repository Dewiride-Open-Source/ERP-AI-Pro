namespace Dewiride.Erp.Modules.Identity.Users.Contracts.People;

// A person's department, designation and manager from a date, in the past or the future, until their next position starts;
// each may be empty from that date.
public sealed record PersonPosition(Guid PersonId, DateOnly EffectiveFrom, Guid? DepartmentId, Guid? DesignationId, Guid? ManagerId);
