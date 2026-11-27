using Dewiride.Erp.BuildingBlocks.Approvals.Domain;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Administration;

public sealed record ApprovalKindSettingDetails(ApprovalKindSettingId Id, bool Required, int WorkingDaysToDecide, string Version);
