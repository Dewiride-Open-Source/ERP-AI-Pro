using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Administration;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;

namespace Dewiride.Erp.Modules.Platform.Approvals.Kinds.Application.Commands.ChangeApprovalKindSetting;

internal sealed record ChangeApprovalKindSettingCommand(ApprovalKindSettingId Id, bool Required, int WorkingDaysToDecide, string Version) : ICommand<ApprovalKindDetails>;
