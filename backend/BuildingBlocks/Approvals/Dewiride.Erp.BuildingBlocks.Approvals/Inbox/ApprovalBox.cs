namespace Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

// The lists of the Approvals page: the requests that wait for the caller's decision, those the caller asked for or submitted, those
// with a step the caller decided, and every request, for a person who may see them all.
public enum ApprovalBox
{
    Waiting = 1,
    Mine = 2,
    Decided = 3,
    All = 4,
}
