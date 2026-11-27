using Dewiride.Erp.BuildingBlocks.Approvals.Domain;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Submitting;

// The latest request of a record: its state, and while it waits, the step it waits at, that step's title and when it is due.
public sealed record ApprovalStatus(ApprovalRequestId RequestId, ApprovalState State, int? CurrentStep, string? StepTitle, DateTimeOffset? DueAt);
