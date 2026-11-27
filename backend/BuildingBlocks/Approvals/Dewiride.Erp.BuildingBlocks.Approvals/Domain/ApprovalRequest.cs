using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Persistence;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Domain;

// One request for approval of one record, saved in the transaction of the module that asks for it, its steps decided one after
// the other. RequestedFor is the person it is for, whose manager decides a manager step, and CreatedBy the person who submitted it;
// neither ever decides it. Every decision and the withdrawal change this row, so its version covers its steps and a decision made
// from an older version is refused. A comment is kept without the spaces around it, and a blank one is no comment.
[EntityTypeConfiguration(typeof(ApprovalRequestConfiguration))]
public sealed class ApprovalRequest : AggregateRoot<ApprovalRequestId>, IAuditable, IVersioned
{
    public const int KindMaxLength = ApprovalKind.KeyMaxLength;

    public const int SubjectTypeMaxLength = ApprovalKind.SubjectTypeMaxLength;

    private readonly List<ApprovalStep> _steps = [];

    private ApprovalRequest(ApprovalRequestId id)
        : base(id)
    {
    }

    public string Kind { get; private set; } = string.Empty;

    public string SubjectType { get; private set; } = string.Empty;

    public Guid SubjectId { get; private set; }

    public Guid RequestedFor { get; private set; }

    public ApprovalState State { get; private set; }

    public int? CurrentStep { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyList<ApprovalStep> Steps => _steps;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public ApprovalStep? WaitingStep => CurrentStep is { } order ? _steps.Find(step => step.Order == order) : null;

    internal static ApprovalRequest Submit(string kind, string subjectType, Guid subjectId, Guid requestedFor, IReadOnlyList<ApprovalStepPlan> steps, DateTimeOffset submittedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectType);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(kind.Length, KindMaxLength, nameof(kind));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(subjectType.Length, SubjectTypeMaxLength, nameof(subjectType));
        if (steps.Count is 0 or > ApprovalKind.MaxSteps)
        {
            throw new ArgumentException($"A request has 1 to {ApprovalKind.MaxSteps} steps.", nameof(steps));
        }

        var request = new ApprovalRequest(ApprovalRequestId.Create())
        {
            Kind = kind,
            SubjectType = subjectType,
            SubjectId = subjectId,
            RequestedFor = requestedFor,
            State = ApprovalState.Submitted,
            SubmittedAt = submittedAt,
        };
        for (var index = 0; index < steps.Count; index++)
        {
            request._steps.Add(ApprovalStep.Plan(request.Id, index + 1, steps[index]));
        }

        request.StartStep(request._steps[0], submittedAt);

        return request;
    }

    internal Result Approve(Guid decidedBy, bool inPlaceOfApprover, string? comment, DateTimeOffset decidedAt)
    {
        if (WaitingStep is not { } step || State != ApprovalState.Submitted)
        {
            return ApprovalErrors.NotWaiting;
        }

        var note = Normalised(comment);
        if (note?.Length > ApprovalStep.CommentMaxLength)
        {
            return ApprovalErrors.CommentTooLong;
        }

        step.Decide(ApprovalStepState.Approved, decidedBy, inPlaceOfApprover, note, decidedAt);
        var next = _steps.Where(later => later.Order > step.Order).MinBy(later => later.Order);
        if (next is null)
        {
            Close(ApprovalState.Approved, decidedAt);
        }
        else
        {
            StartStep(next, decidedAt);
        }

        return Result.Success();
    }

    internal Result Return(Guid decidedBy, bool inPlaceOfApprover, string? reason, DateTimeOffset decidedAt) =>
        End(ApprovalState.Returned, ApprovalStepState.Returned, decidedBy, inPlaceOfApprover, reason, decidedAt);

    internal Result Reject(Guid decidedBy, bool inPlaceOfApprover, string? reason, DateTimeOffset decidedAt) =>
        End(ApprovalState.Rejected, ApprovalStepState.Rejected, decidedBy, inPlaceOfApprover, reason, decidedAt);

    internal Result Withdraw(string? reason, DateTimeOffset withdrawnAt)
    {
        if (WaitingStep is not { } step || State != ApprovalState.Submitted)
        {
            return ApprovalErrors.NotWaiting;
        }

        var note = Normalised(reason);
        if (note?.Length > ApprovalStep.CommentMaxLength)
        {
            return ApprovalErrors.CommentTooLong;
        }

        step.Close(note);
        CloseStepsAfter(step);
        Close(ApprovalState.Withdrawn, withdrawnAt);

        return Result.Success();
    }

    private static string? Normalised(string? comment) => string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

    private Result End(ApprovalState state, ApprovalStepState outcome, Guid decidedBy, bool inPlaceOfApprover, string? reason, DateTimeOffset decidedAt)
    {
        if (WaitingStep is not { } step || State != ApprovalState.Submitted)
        {
            return ApprovalErrors.NotWaiting;
        }

        var note = Normalised(reason);
        if (note is null)
        {
            return ApprovalErrors.ReasonRequired(state);
        }

        if (note.Length > ApprovalStep.CommentMaxLength)
        {
            return ApprovalErrors.CommentTooLong;
        }

        step.Decide(outcome, decidedBy, inPlaceOfApprover, note, decidedAt);
        CloseStepsAfter(step);
        Close(state, decidedAt);

        return Result.Success();
    }

    private void StartStep(ApprovalStep step, DateTimeOffset startedAt)
    {
        step.Start(startedAt, WorkingDays.DueAt(startedAt, step.WorkingDays));
        CurrentStep = step.Order;
    }

    private void CloseStepsAfter(ApprovalStep step)
    {
        foreach (var later in _steps.Where(later => later.Order > step.Order && later.State == ApprovalStepState.Pending))
        {
            later.Close(null);
        }
    }

    private void Close(ApprovalState state, DateTimeOffset closedAt)
    {
        State = state;
        CurrentStep = null;
        ClosedAt = closedAt;
    }
}
