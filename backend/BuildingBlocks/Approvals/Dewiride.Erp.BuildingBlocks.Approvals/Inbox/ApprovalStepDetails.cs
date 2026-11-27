using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

// A step with who it waits for, the working days it has and, once it started, when it is due; a decided step names its decider,
// whether they decided in place of the person or role it waited for, and the note or reason they gave. A step closed by a
// withdrawal keeps the reason given for it and no decider.
public sealed record ApprovalStepDetails(
    int Order,
    string Title,
    ApprovalStepRule Rule,
    ApprovalStepState State,
    ApproverKind ApproverKind,
    Guid? ApproverId,
    string? ApproverName,
    ErpRole? ApproverRole,
    int WorkingDays,
    DateTimeOffset? StartedAt,
    DateTimeOffset? DueAt,
    bool Late,
    Guid? DecidedById,
    string? DecidedByName,
    DateTimeOffset? DecidedAt,
    bool InPlaceOfApprover,
    string? Comment);
