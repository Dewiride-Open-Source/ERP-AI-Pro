namespace Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

// A request as its page shows it: the version a decision is made from, its steps in order, the requests of the same kind for the
// same record submitted before it, newest first, and what the caller may do with it. DecidesInPlace marks a decision the caller
// takes in place of the person or role the request waits for.
public sealed record ApprovalRequestDetails(
    ApprovalRequestSummary Request,
    string Version,
    IReadOnlyList<ApprovalStepDetails> Steps,
    IReadOnlyList<EarlierApprovalRequest> EarlierRequests,
    bool CanDecide,
    bool DecidesInPlace,
    bool CanWithdraw);
