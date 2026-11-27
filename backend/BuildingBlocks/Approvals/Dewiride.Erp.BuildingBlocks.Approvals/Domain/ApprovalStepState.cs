namespace Dewiride.Erp.BuildingBlocks.Approvals.Domain;

// Pending is a step the request has not reached yet, and Closed one that ended without a decision of its own: the request was
// withdrawn, or an earlier step sent it back or rejected it. The numbers are stored, and the filtered indexes of the waiting steps
// name Waiting by its number.
public enum ApprovalStepState
{
    Pending = 1,
    Waiting = 2,
    Approved = 3,
    Returned = 4,
    Rejected = 5,
    Closed = 6,
}
