using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Administration;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;

namespace Dewiride.Erp.Modules.Platform.Approvals.Kinds.Application.Commands.ChangeApprovalKindSetting;

internal sealed class ChangeApprovalKindSettingHandler(IApprovalAdministration administration) : ICommandHandler<ChangeApprovalKindSettingCommand, ApprovalKindDetails>
{
    public Task<Result<ApprovalKindDetails>> HandleAsync(ChangeApprovalKindSettingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return RecordVersion.TryDecode(command.Version, out var version)
            ? administration.ChangeAsync(command.Id, version, command.Required, command.WorkingDaysToDecide, cancellationToken)
            : Task.FromResult<Result<ApprovalKindDetails>>(ApprovalErrors.VersionInvalid);
    }
}
