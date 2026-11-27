using Dewiride.Erp.BuildingBlocks.Approvals.Domain;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

public sealed record EarlierApprovalRequest(ApprovalRequestId Id, ApprovalState State, DateTimeOffset SubmittedAt, DateTimeOffset? ClosedAt);
