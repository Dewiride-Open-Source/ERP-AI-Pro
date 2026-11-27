namespace Dewiride.Erp.BuildingBlocks.Approvals.Domain;

// Who a step waits for, as it was settled when the request was submitted: one person, or every holder of a role. A manager step
// with no manager left to decide waits for the role Administrator.
public enum ApproverKind
{
    Person = 1,
    Role = 2,
}
