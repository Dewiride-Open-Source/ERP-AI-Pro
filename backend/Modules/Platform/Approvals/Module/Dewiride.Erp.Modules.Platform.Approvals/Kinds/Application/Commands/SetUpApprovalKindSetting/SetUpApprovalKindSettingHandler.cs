using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Administration;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Approvals.Kinds.Application.Commands.SetUpApprovalKindSetting;

internal sealed class SetUpApprovalKindSettingHandler(IApprovalAdministration administration) : ICommandHandler<SetUpApprovalKindSettingCommand, ApprovalKindDetails>
{
    public Task<Result<ApprovalKindDetails>> HandleAsync(SetUpApprovalKindSettingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return administration.SetUpAsync(command.Kind, command.Required, command.WorkingDaysToDecide, cancellationToken);
    }
}
