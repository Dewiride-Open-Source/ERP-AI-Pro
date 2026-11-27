using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

// A person sees the requests they asked for or submitted, those that wait for their decision and those with a step they decided,
// and a person who may see them all sees every request; any other request answers as an unknown id does. A list sorts by
// submittedAt, dueAt or closedAt, the waiting list by its due times, late first, and every other list newest first, and filters by
// kind, state and subjectId.
public interface IApprovalInbox
{
    Task<Result<PagedResult<ApprovalRequestSummary>>> ListAsync(ApprovalBox box, ListRequest list, ApprovalCaller caller, CancellationToken cancellationToken);

    Task<Result<ApprovalRequestDetails>> GetAsync(ApprovalRequestId id, ApprovalCaller caller, CancellationToken cancellationToken);

    Task<ApprovalCounts> CountAsync(ApprovalCaller caller, CancellationToken cancellationToken);
}
