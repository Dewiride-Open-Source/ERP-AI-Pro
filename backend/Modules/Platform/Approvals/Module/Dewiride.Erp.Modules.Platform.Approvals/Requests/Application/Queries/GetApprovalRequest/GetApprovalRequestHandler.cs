using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Queries.GetApprovalRequest;

internal sealed class GetApprovalRequestHandler(IApprovalInbox inbox, IActorContext actor) : IQueryHandler<GetApprovalRequestQuery, ApprovalRequestDetails>
{
    public Task<Result<ApprovalRequestDetails>> HandleAsync(GetApprovalRequestQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return inbox.GetAsync(query.Id, query.Access.For(actor.ActorId), cancellationToken);
    }
}
