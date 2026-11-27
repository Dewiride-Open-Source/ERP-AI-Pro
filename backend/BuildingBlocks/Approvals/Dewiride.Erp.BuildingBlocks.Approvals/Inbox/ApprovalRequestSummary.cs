using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

// A request as a list shows it. The kind's title is its key when no release declares the kind, and a name is null when no module
// names that record or person. The waiting step's fields are null once the request is closed: WaitingForId and WaitingForName name
// the person a step waits for, and WaitingForRole the role whose holders decide it; Late is true from the step's due time on.
public sealed record ApprovalRequestSummary(
    ApprovalRequestId Id,
    string Kind,
    string KindTitle,
    string SubjectType,
    Guid SubjectId,
    string? SubjectName,
    Guid RequestedForId,
    string? RequestedForName,
    Guid SubmittedById,
    string? SubmittedByName,
    DateTimeOffset SubmittedAt,
    ApprovalState State,
    string? WaitingStepTitle,
    ApproverKind? WaitingForKind,
    Guid? WaitingForId,
    string? WaitingForName,
    ErpRole? WaitingForRole,
    DateTimeOffset? DueAt,
    bool Late,
    DateTimeOffset? ClosedAt);
