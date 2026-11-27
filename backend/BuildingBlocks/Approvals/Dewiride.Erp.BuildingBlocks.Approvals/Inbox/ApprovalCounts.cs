namespace Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

// The requests that wait for the caller's decision, how many of those are late, and how many the caller asked for or submitted
// still wait.
public sealed record ApprovalCounts(int WaitingForMe, int LateForMe, int MineWaiting);
