namespace Dewiride.Erp.BuildingBlocks.Approvals.Domain;

// What a kind asks of a step: the manager of the person the request is for, or a holder of a built-in role.
public enum ApprovalStepRule
{
    Manager = 1,
    Role = 2,
}
