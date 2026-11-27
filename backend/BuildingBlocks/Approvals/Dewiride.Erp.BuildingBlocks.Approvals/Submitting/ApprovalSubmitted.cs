using Dewiride.Erp.BuildingBlocks.Approvals.Domain;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Submitting;

// Submitted with the new request, or Approved without one while administrators have switched approval off for the kind.
public sealed record ApprovalSubmitted(ApprovalRequestId? RequestId, ApprovalState State);
