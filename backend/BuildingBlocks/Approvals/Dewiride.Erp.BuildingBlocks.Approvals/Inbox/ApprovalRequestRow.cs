using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

// A request with its waiting step, the shape a list filters, sorts and pages in SQL; it is built with an object initializer, because
// EF Core translates only the members a projection assigns.
internal sealed class ApprovalRequestRow
{
    public ApprovalRequestId Id { get; init; }

    public string Kind { get; init; } = string.Empty;

    public string SubjectType { get; init; } = string.Empty;

    public Guid SubjectId { get; init; }

    public Guid RequestedFor { get; init; }

    public Guid SubmittedBy { get; init; }

    public DateTimeOffset SubmittedAt { get; init; }

    public ApprovalState State { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    public string? WaitingStepTitle { get; init; }

    public ApproverKind? WaitingForKind { get; init; }

    public Guid? WaitingForId { get; init; }

    public ErpRole? WaitingForRole { get; init; }

    public DateTimeOffset? DueAt { get; init; }
}
