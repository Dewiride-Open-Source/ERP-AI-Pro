using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Approvals.UnitTests.Requests.Application;

internal sealed class RecordingApprovalInbox : IApprovalInbox
{
    public List<string> Calls { get; } = [];

    public ApprovalBox? Box { get; private set; }

    public ListRequest? List { get; private set; }

    public ApprovalRequestId? Id { get; private set; }

    public ApprovalCaller? Caller { get; private set; }

    public Task<Result<PagedResult<ApprovalRequestSummary>>> ListAsync(ApprovalBox box, ListRequest list, ApprovalCaller caller, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ListAsync));
        Box = box;
        List = list;
        Caller = caller;

        return Task.FromResult<Result<PagedResult<ApprovalRequestSummary>>>(ApprovalSamples.Page);
    }

    public Task<Result<ApprovalRequestDetails>> GetAsync(ApprovalRequestId id, ApprovalCaller caller, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(GetAsync));
        Id = id;
        Caller = caller;

        return Task.FromResult<Result<ApprovalRequestDetails>>(ApprovalSamples.Details);
    }

    public Task<ApprovalCounts> CountAsync(ApprovalCaller caller, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(CountAsync));
        Caller = caller;

        return Task.FromResult(ApprovalSamples.Counts);
    }
}
