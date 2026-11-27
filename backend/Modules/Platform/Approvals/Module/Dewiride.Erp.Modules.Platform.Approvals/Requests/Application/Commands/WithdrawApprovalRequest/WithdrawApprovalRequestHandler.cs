using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Commands.WithdrawApprovalRequest;

internal sealed class WithdrawApprovalRequestHandler(IApprovalDecisions decisions, IActorContext actor) : ICommandHandler<WithdrawApprovalRequestCommand, ApprovalRequestDetails>
{
    public Task<Result<ApprovalRequestDetails>> HandleAsync(WithdrawApprovalRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return RecordVersion.TryDecode(command.Version, out var version)
            ? decisions.WithdrawAsync(command.Id, command.Comment, version, command.Access.For(actor.ActorId), cancellationToken)
            : Task.FromResult<Result<ApprovalRequestDetails>>(ApprovalErrors.VersionInvalid);
    }
}
