using Dewiride.Erp.BuildingBlocks.Approvals.Domain;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Outcomes;

// DecidedBy is the person whose decision or withdrawal closed the request, and Comment the note or the reason they gave with it.
public sealed record ApprovalOutcome(string Kind, Guid SubjectId, ApprovalRequestId RequestId, ApprovalState State, Guid DecidedBy, string? Comment);
