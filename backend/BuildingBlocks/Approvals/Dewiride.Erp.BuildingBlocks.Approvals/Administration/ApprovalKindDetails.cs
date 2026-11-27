using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Administration;

// A kind as this release declares it, with the administrators' setting, null until one is set up: until then the kind needs
// approval and gives each step the working days it declares.
public sealed record ApprovalKindDetails(ApprovalKind Kind, ApprovalKindSettingDetails? Setting);
