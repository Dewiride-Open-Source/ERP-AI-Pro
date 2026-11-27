using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Deciding;

// A decision is taken in a unit of work on the kind's own context, under the request's lock and from the version the decider last
// read, and one that closes the request calls the kind's outcome handler in that same transaction, so the record moves with the
// request or nothing changes. Sending back and rejecting need a reason. A withdrawal on the Approvals page is the person's the
// request is for or the person's who submitted it, and its outcome handler is told the request was withdrawn. The answer is the
// request as the caller sees it afterwards.
public interface IApprovalDecisions
{
    Task<Result<ApprovalRequestDetails>> DecideAsync(ApprovalRequestId id, ApprovalDecision decision, string? comment, byte[] version, ApprovalCaller caller, CancellationToken cancellationToken);

    Task<Result<ApprovalRequestDetails>> WithdrawAsync(ApprovalRequestId id, string? comment, byte[] version, ApprovalCaller caller, CancellationToken cancellationToken);
}
