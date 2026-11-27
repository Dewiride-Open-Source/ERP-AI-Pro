using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Commands.DecideApprovalRequest;

internal sealed class DecideApprovalRequestHandler(IApprovalDecisions decisions, IActorContext actor) : ICommandHandler<DecideApprovalRequestCommand, ApprovalRequestDetails>
{
    public Task<Result<ApprovalRequestDetails>> HandleAsync(DecideApprovalRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return RecordVersion.TryDecode(command.Version, out var version)
            ? decisions.DecideAsync(command.Id, command.Decision, command.Comment, version, command.Access.For(actor.ActorId), cancellationToken)
            : Task.FromResult<Result<ApprovalRequestDetails>>(ApprovalErrors.VersionInvalid);
    }
}
