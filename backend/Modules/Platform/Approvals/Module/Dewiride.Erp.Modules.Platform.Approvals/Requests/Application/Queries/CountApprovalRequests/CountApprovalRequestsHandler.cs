using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Queries.CountApprovalRequests;

internal sealed class CountApprovalRequestsHandler(IApprovalInbox inbox, IActorContext actor) : IQueryHandler<CountApprovalRequestsQuery, ApprovalCounts>
{
    public async Task<Result<ApprovalCounts>> HandleAsync(CountApprovalRequestsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Result.Success(await inbox.CountAsync(query.Access.For(actor.ActorId), cancellationToken).ConfigureAwait(false));
    }
}
