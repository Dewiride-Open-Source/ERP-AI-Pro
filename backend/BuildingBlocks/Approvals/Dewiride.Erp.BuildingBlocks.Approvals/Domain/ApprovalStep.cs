using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Persistence;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Domain;

// One step of a request, with who it waits for as its submission settled and the working days it has. The first step starts
// waiting at the submission and each later one when the step before it is approved, and its due time counts from then. A step
// that ended without a decision of its own keeps no decider; the reason given for a withdrawal stays on the step that was waiting.
[EntityTypeConfiguration(typeof(ApprovalStepConfiguration))]
public sealed class ApprovalStep : Entity<ApprovalStepId>, IAuditable
{
    public const int TitleMaxLength = ApprovalStepDefinition.TitleMaxLength;

    public const int CommentMaxLength = 1000;

    private ApprovalStep(ApprovalStepId id)
        : base(id)
    {
    }

    public ApprovalRequestId RequestId { get; private set; }

    public int Order { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public ApprovalStepRule Rule { get; private set; }

    public ApproverKind ApproverKind { get; private set; }

    public Guid? ApproverId { get; private set; }

    public ErpRole? ApproverRole { get; private set; }

    public int WorkingDays { get; private set; }

    public ApprovalStepState State { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? DueAt { get; private set; }

    public Guid? DecidedBy { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public bool InPlaceOfApprover { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public bool IsLateAt(DateTimeOffset now) => State == ApprovalStepState.Waiting && DueAt is { } due && now >= due;

    internal static ApprovalStep Plan(ApprovalRequestId requestId, int order, ApprovalStepPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentOutOfRangeException.ThrowIfLessThan(order, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(plan.WorkingDays, ApprovalKind.MinWorkingDays);

        return new ApprovalStep(ApprovalStepId.Create())
        {
            RequestId = requestId,
            Order = order,
            Title = plan.Title,
            Rule = plan.Rule,
            ApproverKind = plan.ApproverKind,
            ApproverId = plan.ApproverId,
            ApproverRole = plan.ApproverRole,
            WorkingDays = plan.WorkingDays,
            State = ApprovalStepState.Pending,
        };
    }

    internal void Start(DateTimeOffset startedAt, DateTimeOffset dueAt)
    {
        State = ApprovalStepState.Waiting;
        StartedAt = startedAt;
        DueAt = dueAt;
    }

    internal void Decide(ApprovalStepState outcome, Guid decidedBy, bool inPlaceOfApprover, string? comment, DateTimeOffset decidedAt)
    {
        if (outcome is not (ApprovalStepState.Approved or ApprovalStepState.Returned or ApprovalStepState.Rejected))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "A step is decided by approving, sending back or rejecting it.");
        }

        State = outcome;
        DecidedBy = decidedBy;
        DecidedAt = decidedAt;
        InPlaceOfApprover = inPlaceOfApprover;
        Comment = comment;
    }

    internal void Close(string? comment)
    {
        State = ApprovalStepState.Closed;
        Comment = comment;
    }
}
