using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Administration;

namespace Dewiride.Erp.Modules.Platform.Approvals.Kinds.Application.Commands.SetUpApprovalKindSetting;

internal sealed record SetUpApprovalKindSettingCommand(string Kind, bool Required, int WorkingDaysToDecide) : ICommand<ApprovalKindDetails>;
