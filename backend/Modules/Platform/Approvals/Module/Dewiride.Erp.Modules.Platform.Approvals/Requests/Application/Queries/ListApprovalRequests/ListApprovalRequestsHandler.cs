using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Queries.ListApprovalRequests;

internal sealed class ListApprovalRequestsHandler(IApprovalInbox inbox, IActorContext actor) : IQueryHandler<ListApprovalRequestsQuery, PagedResult<ApprovalRequestSummary>>
{
    public Task<Result<PagedResult<ApprovalRequestSummary>>> HandleAsync(ListApprovalRequestsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return inbox.ListAsync(query.Box, query.List, query.Access.For(actor.ActorId), cancellationToken);
    }
}
