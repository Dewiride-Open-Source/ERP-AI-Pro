using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Approvals;

public sealed class ApprovalRequestTests
{
    private static readonly DateTimeOffset MondayMorning = new(2026, 11, 23, 4, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset TuesdayNoon = new(2026, 11, 24, 6, 30, 0, TimeSpan.Zero);

    private static readonly Guid Asker = new("01999a00-0000-7000-8000-000000000001");

    private static readonly Guid Manager = new("01999a00-0000-7000-8000-000000000002");

    private static readonly Guid Accountant = new("01999a00-0000-7000-8000-000000000003");

    private static readonly string LongestComment = new('a', ApprovalStep.CommentMaxLength);

    [Fact]
    public void Submit_ThreeSteps_StartsTheFirstAndLeavesTheOthersPending()
    {
        var request = ThreeSteps();

        Assert.Equal(ApprovalState.Submitted, request.State);
        Assert.Equal(1, request.CurrentStep);
        Assert.Equal(MondayMorning, request.SubmittedAt);
        Assert.Null(request.ClosedAt);
        Assert.Equal([1, 2, 3], request.Steps.Select(step => step.Order));
        Assert.All(request.Steps, step => Assert.Equal(request.Id, step.RequestId));
        var first = request.Steps[0];
        Assert.Same(first, request.WaitingStep);
        Assert.Equal((ApprovalStepState.Waiting, ApproverKind.Person, (Guid?)Manager), (first.State, first.ApproverKind, first.ApproverId));
        Assert.Equal(MondayMorning, first.StartedAt);
        Assert.Equal(IndianStandardTime.StartOfIstDay(new DateOnly(2026, 11, 26)), first.DueAt);
        Assert.All(request.Steps.Skip(1), step =>
        {
            Assert.Equal(ApprovalStepState.Pending, step.State);
            Assert.Null(step.StartedAt);
            Assert.Null(step.DueAt);
            Assert.Equal(ApproverKind.Role, step.ApproverKind);
        });
    }

    [Fact]
    public void Approve_AStepBeforeTheLast_StartsTheNextFromTheDecision()
    {
        var request = ThreeSteps();

        var approved = request.Approve(Manager, inPlaceOfApprover: false, "  Looks right  ", TuesdayNoon);

        Assert.True(approved.IsSuccess);
        Assert.Equal(ApprovalState.Submitted, request.State);
        Assert.Equal(2, request.CurrentStep);
        Assert.Null(request.ClosedAt);
        var first = request.Steps[0];
        Assert.Equal((ApprovalStepState.Approved, (Guid?)Manager, (DateTimeOffset?)TuesdayNoon, false, "Looks right"), (first.State, first.DecidedBy, first.DecidedAt, first.InPlaceOfApprover, first.Comment));
        var second = Assert.IsType<ApprovalStep>(request.WaitingStep);
        Assert.Equal(2, second.Order);
        Assert.Equal(TuesdayNoon, second.StartedAt);
        Assert.Equal(IndianStandardTime.StartOfIstDay(new DateOnly(2026, 11, 27)), second.DueAt);
        Assert.Equal(ApprovalStepState.Pending, request.Steps[2].State);
    }

    [Fact]
    public void Approve_TheLastStep_ApprovesTheRequest()
    {
        var request = SubmittedRequests.Of(Asker, Asker, MondayMorning, SubmittedRequests.ManagerStep(Manager));

        var approved = request.Approve(Manager, inPlaceOfApprover: true, comment: null, TuesdayNoon);

        Assert.True(approved.IsSuccess);
        Assert.Equal((ApprovalState.Approved, (int?)null, (DateTimeOffset?)TuesdayNoon), (request.State, request.CurrentStep, request.ClosedAt));
        Assert.Null(request.WaitingStep);
        var step = Assert.Single(request.Steps);
        Assert.Equal((ApprovalStepState.Approved, true, (string?)null), (step.State, step.InPlaceOfApprover, step.Comment));
    }

    [Theory]
    [InlineData(ApprovalState.Returned, ApprovalStepState.Returned)]
    [InlineData(ApprovalState.Rejected, ApprovalStepState.Rejected)]
    public void ReturnOrReject_WithAReason_ClosesTheRequestAndItsLaterSteps(ApprovalState closing, ApprovalStepState decided)
    {
        var request = ThreeSteps();

        var ended = End(request, closing, Manager, " The receipt is missing. ");

        Assert.True(ended.IsSuccess);
        Assert.Equal((closing, (int?)null, (DateTimeOffset?)TuesdayNoon), (request.State, request.CurrentStep, request.ClosedAt));
        Assert.Equal((decided, (Guid?)Manager, "The receipt is missing."), (request.Steps[0].State, request.Steps[0].DecidedBy, request.Steps[0].Comment));
        Assert.All(request.Steps.Skip(1), step => Assert.Equal((ApprovalStepState.Closed, (Guid?)null, (string?)null), (step.State, step.DecidedBy, step.Comment)));
    }

    [Theory]
    [InlineData(ApprovalState.Returned, null, "Say what needs to change.")]
    [InlineData(ApprovalState.Returned, "   ", "Say what needs to change.")]
    [InlineData(ApprovalState.Rejected, "", "Say why it is rejected.")]
    public void ReturnOrReject_WithoutAReason_IsRefusedAndChangesNothing(ApprovalState closing, string? reason, string message)
    {
        var request = ThreeSteps();

        var ended = End(request, closing, Manager, reason);

        Assert.Equal("approval.reason-required", ended.Error!.Code);
        Assert.Equal([message], ended.Error.Fields[ApprovalErrors.CommentField]);
        Assert.Equal((ApprovalState.Submitted, 1, ApprovalStepState.Waiting), (request.State, request.CurrentStep, request.Steps[0].State));
    }

    [Fact]
    public void Decide_ACommentOfAThousandCharacters_IsKeptAndALongerOneIsRefused()
    {
        var kept = ThreeSteps();
        var refused = ThreeSteps();

        Assert.True(kept.Approve(Manager, inPlaceOfApprover: false, LongestComment, TuesdayNoon).IsSuccess);
        Assert.Equal(LongestComment, kept.Steps[0].Comment);
        Assert.Equal("approval.comment-too-long", refused.Approve(Manager, inPlaceOfApprover: false, LongestComment + "a", TuesdayNoon).Error!.Code);
        Assert.Equal("approval.comment-too-long", refused.Reject(Manager, inPlaceOfApprover: false, LongestComment + "a", TuesdayNoon).Error!.Code);
        Assert.Equal("approval.comment-too-long", refused.Withdraw(LongestComment + "a", TuesdayNoon).Error!.Code);
        Assert.Equal(ApprovalState.Submitted, refused.State);
    }

    [Fact]
    public void Withdraw_AWaitingRequest_ClosesItsStepsAndKeepsTheReasonOnTheWaitingStep()
    {
        var request = ThreeSteps();
        Assert.True(request.Approve(Manager, inPlaceOfApprover: false, comment: null, TuesdayNoon).IsSuccess);

        var withdrawn = request.Withdraw(" Sent by mistake ", TuesdayNoon.AddHours(1));

        Assert.True(withdrawn.IsSuccess);
        Assert.Equal((ApprovalState.Withdrawn, (int?)null, (DateTimeOffset?)TuesdayNoon.AddHours(1)), (request.State, request.CurrentStep, request.ClosedAt));
        Assert.Equal(ApprovalStepState.Approved, request.Steps[0].State);
        Assert.Equal((ApprovalStepState.Closed, (Guid?)null, "Sent by mistake"), (request.Steps[1].State, request.Steps[1].DecidedBy, request.Steps[1].Comment));
        Assert.Equal((ApprovalStepState.Closed, (string?)null), (request.Steps[2].State, request.Steps[2].Comment));
    }

    [Theory]
    [InlineData(ApprovalState.Approved)]
    [InlineData(ApprovalState.Returned)]
    [InlineData(ApprovalState.Rejected)]
    [InlineData(ApprovalState.Withdrawn)]
    public void EveryChange_OfAClosedRequest_IsRefused(ApprovalState closed)
    {
        var request = SubmittedRequests.Of(Asker, Asker, MondayMorning, SubmittedRequests.ManagerStep(Manager));
        var closing = closed switch
        {
            ApprovalState.Approved => request.Approve(Manager, inPlaceOfApprover: false, comment: null, TuesdayNoon),
            ApprovalState.Withdrawn => request.Withdraw(reason: null, TuesdayNoon),
            _ => End(request, closed, Manager, "Done."),
        };
        Assert.True(closing.IsSuccess);

        Result[] attempts =
        [
            request.Approve(Manager, inPlaceOfApprover: false, comment: null, TuesdayNoon),
            request.Return(Manager, inPlaceOfApprover: false, "Change it.", TuesdayNoon),
            request.Reject(Manager, inPlaceOfApprover: false, "No.", TuesdayNoon),
            request.Withdraw(reason: null, TuesdayNoon),
        ];

        Assert.All(attempts, attempt => Assert.Equal(ApprovalErrors.NotWaiting, attempt.Error));
        Assert.Equal(closed, request.State);
    }

    [Fact]
    public void IsLateAt_TheWaitingStepsDueTimeOnward_IsLateAndAClosedStepNever()
    {
        var request = SubmittedRequests.Of(Asker, Asker, MondayMorning, SubmittedRequests.ManagerStep(Manager), SubmittedRequests.RoleStep("Finance", ErpRole.Finance));
        var first = request.Steps[0];
        var due = first.DueAt!.Value;

        Assert.False(first.IsLateAt(due.AddTicks(-1)));
        Assert.True(first.IsLateAt(due));
        Assert.False(request.Steps[1].IsLateAt(due.AddDays(30)));
        Assert.True(request.Approve(Accountant, inPlaceOfApprover: true, comment: null, due.AddDays(1)).IsSuccess);
        Assert.False(first.IsLateAt(due.AddDays(30)));
    }

    private static ApprovalRequest ThreeSteps() =>
        SubmittedRequests.Of(
            Asker,
            Asker,
            MondayMorning,
            SubmittedRequests.ManagerStep(Manager),
            SubmittedRequests.RoleStep("Finance", ErpRole.Finance),
            SubmittedRequests.RoleStep("Administrators", ErpRole.Administrator));

    private static Result End(ApprovalRequest request, ApprovalState closing, Guid decidedBy, string? reason) =>
        closing == ApprovalState.Returned
            ? request.Return(decidedBy, inPlaceOfApprover: false, reason, TuesdayNoon)
            : request.Reject(decidedBy, inPlaceOfApprover: false, reason, TuesdayNoon);
}
