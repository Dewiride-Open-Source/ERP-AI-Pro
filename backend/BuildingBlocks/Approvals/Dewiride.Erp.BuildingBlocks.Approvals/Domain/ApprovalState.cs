namespace Dewiride.Erp.BuildingBlocks.Approvals.Domain;

// Every state but Submitted is final: a record sent again after a change is asked for in a new request, so no request is decided
// twice. The numbers are stored, and the filtered unique index of the waiting requests names Submitted by its number.
public enum ApprovalState
{
    Submitted = 1,
    Approved = 2,
    Returned = 3,
    Rejected = 4,
    Withdrawn = 5,
}
