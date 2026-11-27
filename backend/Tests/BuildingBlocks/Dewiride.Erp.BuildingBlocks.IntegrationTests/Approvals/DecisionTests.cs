using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample.Commands;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample.Integration;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using static Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.ApprovalsDatabase;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals;

public sealed class DecisionTests
{
    [Fact]
    public async Task DecideAsync_ApprovingTheOnlyStep_ApprovesTheRequestAndTheRecordTogether()
    {
        await using var database = await CreateAsync();
        var (asker, manager) = (NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (claim, requestId) = await database.SubmittedClaimAsync(database.ManagerKind, asker);
        database.Clock.Advance(TimeSpan.FromHours(3));

        var decided = await database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(manager), "  Paid from the travel budget.  ");

        Assert.Equal((ApprovalState.Approved, false, false), (decided.Value.Request.State, decided.Value.CanDecide, decided.Value.CanWithdraw));
        var request = await database.RequestAsync(requestId);
        Assert.Equal((ApprovalState.Approved, (int?)null, (DateTimeOffset?)Today.AddHours(3)), (request.State, request.CurrentStep, request.ClosedAt));
        var step = Assert.Single(request.Steps);
        Assert.Equal((ApprovalStepState.Approved, (Guid?)manager, (DateTimeOffset?)Today.AddHours(3), false, "Paid from the travel budget."), (step.State, step.DecidedBy, step.DecidedAt, step.InPlaceOfApprover, step.Comment));
        Assert.Equal(SampleClaimState.Approved, (await database.ClaimAsync(claim)).State);
        var outcome = Assert.Single(database.Script.Received);
        Assert.Equal((database.ManagerKind.Key, claim.Value, requestId, ApprovalState.Approved, manager, "Paid from the travel budget."), (outcome.Kind, outcome.SubjectId, outcome.RequestId, outcome.State, outcome.DecidedBy, outcome.Comment));
    }

    [Fact]
    public async Task DecideAsync_ApprovingTheFirstOfTwoSteps_StartsTheSecondAndLeavesTheRecordWaiting()
    {
        await using var database = await CreateAsync();
        var (asker, manager) = (NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (claim, requestId) = await database.SubmittedClaimAsync(database.ChainKind, asker);
        database.Clock.Advance(TimeSpan.FromDays(3));

        Assert.True((await database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(manager))).IsSuccess);

        var request = await database.RequestAsync(requestId);
        Assert.Equal((ApprovalState.Submitted, (int?)2), (request.State, request.CurrentStep));
        var second = request.Steps.Single(step => step.Order == 2);
        Assert.Equal((ApprovalStepState.Waiting, (DateTimeOffset?)Today.AddDays(3)), (second.State, second.StartedAt));
        Assert.Equal(IndianStandardTime.StartOfIstDay(new DateOnly(2026, 12, 3)), second.DueAt);
        Assert.Equal(SampleClaimState.Waiting, (await database.ClaimAsync(claim)).State);
        Assert.Empty(database.Script.Received);
    }

    [Theory]
    [InlineData(ApprovalDecision.Return, ApprovalState.Returned, ApprovalStepState.Returned, SampleClaimState.SentBack)]
    [InlineData(ApprovalDecision.Reject, ApprovalState.Rejected, ApprovalStepState.Rejected, SampleClaimState.Rejected)]
    public async Task DecideAsync_SendingBackOrRejectingWithAReason_ClosesTheRequestAndMovesTheRecord(ApprovalDecision decision, ApprovalState closed, ApprovalStepState decided, SampleClaimState claimState)
    {
        await using var database = await CreateAsync();
        var (asker, manager) = (NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (claim, requestId) = await database.SubmittedClaimAsync(database.ChainKind, asker);

        Assert.True((await database.DecideAsync(requestId, decision, Caller(manager), "The receipt is for another trip.")).IsSuccess);

        var request = await database.RequestAsync(requestId);
        Assert.Equal(closed, request.State);
        Assert.Equal((decided, "The receipt is for another trip."), (request.Steps.Single(step => step.Order == 1).State, request.Steps.Single(step => step.Order == 1).Comment));
        Assert.Equal(ApprovalStepState.Closed, request.Steps.Single(step => step.Order == 2).State);
        Assert.Equal(claimState, (await database.ClaimAsync(claim)).State);
        Assert.Equal(closed, Assert.Single(database.Script.Received).State);
    }

    [Theory]
    [InlineData(ApprovalDecision.Return, null)]
    [InlineData(ApprovalDecision.Reject, "  ")]
    public async Task DecideAsync_SendingBackOrRejectingWithoutAReason_IsRefusedAndChangesNothing(ApprovalDecision decision, string? reason)
    {
        await using var database = await CreateAsync();
        var (asker, manager) = (NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (claim, requestId) = await database.SubmittedClaimAsync(database.ManagerKind, asker);
        var version = await database.VersionOfAsync(requestId);

        var refused = await database.DecideAsync(requestId, decision, Caller(manager), reason);

        Assert.Equal(("approval.reason-required", ErrorKind.Validation), (refused.Error!.Code, refused.Error.Kind));
        Assert.Contains(ApprovalErrors.CommentField, refused.Error.Fields.Keys);
        Assert.Equal(version, await database.VersionOfAsync(requestId));
        Assert.Equal(SampleClaimState.Waiting, (await database.ClaimAsync(claim)).State);
    }

    [Fact]
    public async Task DecideAsync_AnApprovalTheModuleRefuses_RollsTheDecisionBackAndTheRequestCanStillBeSentBack()
    {
        await using var database = await CreateAsync();
        var (asker, manager) = (NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (claim, requestId) = await database.SubmittedClaimAsync(database.ManagerKind, asker, receiptAttached: false);
        var version = await database.VersionOfAsync(requestId);

        var refused = await database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(manager));

        Assert.Equal((ApplySampleClaimOutcomeHandler.ReceiptMissingCode, ErrorKind.Failure), (refused.Error!.Code, refused.Error.Kind));
        var request = await database.RequestAsync(requestId);
        Assert.Equal((ApprovalState.Submitted, ApprovalStepState.Waiting), (request.State, Assert.Single(request.Steps).State));
        Assert.Equal(version, request.RowVersion);
        Assert.Equal(SampleClaimState.Waiting, (await database.ClaimAsync(claim)).State);

        Assert.True((await database.DecideAsync(requestId, ApprovalDecision.Return, Caller(manager), "Attach the receipt.")).IsSuccess);
        Assert.Equal(SampleClaimState.SentBack, (await database.ClaimAsync(claim)).State);
    }

    [Fact]
    public async Task DecideAsync_FromAnOlderVersion_IsRefusedAndFromTheLatestTakesTheNextStep()
    {
        await using var database = await CreateAsync();
        var (asker, manager, accountant) = (NewPerson(), NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (_, requestId) = await database.SubmittedClaimAsync(database.ChainKind, asker);
        var older = await database.VersionOfAsync(requestId);
        Assert.True((await database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(manager))).IsSuccess);

        var stale = await database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(accountant, ErpRole.Finance), version: older);
        var latest = await database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(accountant, ErpRole.Finance));

        Assert.Equal(("concurrency.conflict", ErrorKind.Conflict), (stale.Error!.Code, stale.Error.Kind));
        Assert.Equal(ApprovalState.Approved, latest.Value.Request.State);
    }

    [Fact]
    public async Task DecideAsync_ARequestNoLongerWaiting_IsRefused()
    {
        await using var database = await CreateAsync();
        var (asker, manager) = (NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (_, requestId) = await database.SubmittedClaimAsync(database.ManagerKind, asker);
        Assert.True((await database.DecideAsync(requestId, ApprovalDecision.Reject, Caller(manager), "Not a business trip.")).IsSuccess);

        var again = await database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(manager));

        Assert.Equal(("approval.not-waiting", ErrorKind.Conflict), (again.Error!.Code, again.Error.Kind));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DecideAsync_ByThePersonItIsForOrWhoSubmittedIt_IsRefusedEvenForAnAdministrator(bool asker)
    {
        await using var database = await CreateAsync();
        var (person, submitter) = (NewPerson(), NewPerson());
        var (claim, requestId) = await database.SubmittedClaimAsync(database.ManagerKind, person, submitter);

        var refused = await database.DecideAsync(requestId, ApprovalDecision.Approve, Administrator(asker ? person : submitter));

        Assert.Equal(("approval.own-request", ErrorKind.Forbidden), (refused.Error!.Code, refused.Error.Kind));
        Assert.Equal(SampleClaimState.Waiting, (await database.ClaimAsync(claim)).State);
    }

    [Fact]
    public async Task DecideAsync_ByAPersonWhoSeesTheRequestButIsNotItsApprover_IsRefused()
    {
        await using var database = await CreateAsync();
        var (asker, manager) = (NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (_, requestId) = await database.SubmittedClaimAsync(database.ChainKind, asker);
        Assert.True((await database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(manager))).IsSuccess);

        var refused = await database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(manager));

        Assert.Equal(("approval.not-yours", ErrorKind.Forbidden), (refused.Error!.Code, refused.Error.Kind));
    }

    [Fact]
    public async Task DecideAsync_ByAPersonWhoMayNotSeeTheRequest_AnswersAsAnUnknownId()
    {
        await using var database = await CreateAsync();
        var (asker, manager) = (NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (_, requestId) = await database.SubmittedClaimAsync(database.ChainKind, asker);

        var hidden = await database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(NewPerson(), ErpRole.Finance));
        var unknown = await database.DecideAsync(ApprovalRequestId.Create(), ApprovalDecision.Approve, Caller(manager), version: await database.VersionOfAsync(requestId));

        Assert.Equal(ApprovalErrors.RequestNotFound, hidden.Error);
        Assert.Equal(ApprovalErrors.RequestNotFound, unknown.Error);
    }

    [Fact]
    public async Task DecideAsync_ByAnAdministratorInPlaceOfTheManager_MarksTheStepDecidedInPlace()
    {
        await using var database = await CreateAsync();
        var (asker, manager, administrator) = (NewPerson(), NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (_, requestId) = await database.SubmittedClaimAsync(database.ManagerKind, asker);

        var before = await database.InboxAsync(Administrator(administrator), (inbox, token) => inbox.GetAsync(requestId, Administrator(administrator), token));
        var decided = await database.DecideAsync(requestId, ApprovalDecision.Approve, Administrator(administrator));

        Assert.Equal((true, true, false), (before.Value.CanDecide, before.Value.DecidesInPlace, before.Value.CanWithdraw));
        var step = Assert.Single(decided.Value.Steps);
        Assert.Equal((ApprovalStepState.Approved, (Guid?)administrator, true), (step.State, step.DecidedById, step.InPlaceOfApprover));
        Assert.Equal((Guid?)manager, step.ApproverId);
    }

    [Fact]
    public async Task DecideAsync_TheManagerWhoAlsoHoldsTheLaterStepsRole_DecidesBothSteps()
    {
        await using var database = await CreateAsync();
        var (asker, manager) = (NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (claim, requestId) = await database.SubmittedClaimAsync(database.ChainKind, asker);

        Assert.True((await database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(manager, ErpRole.Finance))).IsSuccess);
        var decided = await database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(manager, ErpRole.Finance));

        Assert.Equal(ApprovalState.Approved, decided.Value.Request.State);
        Assert.All(decided.Value.Steps, step => Assert.Equal((ApprovalStepState.Approved, (Guid?)manager, false), (step.State, step.DecidedById, step.InPlaceOfApprover)));
        Assert.Equal(SampleClaimState.Approved, (await database.ClaimAsync(claim)).State);
    }

    [Fact]
    public async Task DecideAsync_ARequestOfAKindThisReleaseDoesNotDeclare_IsRefused()
    {
        await using var database = await CreateAsync();
        await using var other = await CreateAsync();
        var (asker, administrator) = (NewPerson(), NewPerson());
        var (_, requestId) = await other.SubmittedClaimAsync(other.FinanceKind, asker);

        var refused = await database.DecideAsync(requestId, ApprovalDecision.Approve, Administrator(administrator));

        Assert.Equal(("approval.kind-retired", ErrorKind.Failure), (refused.Error!.Code, refused.Error.Kind));
        Assert.Equal(ApprovalState.Submitted, (await database.RequestAsync(requestId)).State);
    }

    [Fact]
    public async Task DecideAsync_AnOutcomeHandlerRefusingASendBack_ThrowsAndChangesNothing()
    {
        await using var database = await CreateAsync();
        var (asker, accountant) = (NewPerson(), NewPerson());
        var (claim, requestId) = await database.SubmittedClaimAsync(database.FinanceKind, asker);
        database.Script.Misbehaviour = SampleClaimMisbehaviour.RefuseEveryOutcome;

        var approval = await database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(accountant, ErpRole.Finance));
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => database.DecideAsync(requestId, ApprovalDecision.Return, Caller(accountant, ErpRole.Finance), "Wrong cost centre."));

        Assert.Equal(SampleClaimOutcomes.RefusedCode, approval.Error!.Code);
        Assert.Contains(SampleClaimOutcomes.RefusedCode, thrown.Message, StringComparison.Ordinal);
        Assert.Equal(ApprovalState.Submitted, (await database.RequestAsync(requestId)).State);
        Assert.Equal(SampleClaimState.Waiting, (await database.ClaimAsync(claim)).State);
    }

    [Theory]
    [InlineData(SampleClaimMisbehaviour.AskForApproval)]
    [InlineData(SampleClaimMisbehaviour.Withdraw)]
    public async Task DecideAsync_AnOutcomeHandlerAskingForOrWithdrawingAnApproval_ThrowsAndChangesNothing(SampleClaimMisbehaviour misbehaviour)
    {
        await using var database = await CreateAsync();
        var (asker, accountant) = (NewPerson(), NewPerson());
        var (claim, requestId) = await database.SubmittedClaimAsync(database.FinanceKind, asker);
        database.Script.Misbehaviour = misbehaviour;

        await Assert.ThrowsAsync<InvalidOperationException>(() => database.DecideAsync(requestId, ApprovalDecision.Return, Caller(accountant, ErpRole.Finance), "Wrong project."));

        Assert.Equal(ApprovalState.Submitted, Assert.Single(await database.RequestsOfAsync(claim.Value)).State);
        Assert.Equal(SampleClaimState.Waiting, (await database.ClaimAsync(claim)).State);
    }

    [Fact]
    public async Task DecideAsync_ADecision_LogsTheRequestItsKindItsStateAndTheStepWithoutTheComment()
    {
        await using var database = await CreateAsync();
        var (asker, accountant) = (NewPerson(), NewPerson());
        var (_, requestId) = await database.SubmittedClaimAsync(database.FinanceKind, asker);

        Assert.True((await database.DecideAsync(requestId, ApprovalDecision.Reject, Caller(accountant, ErpRole.Finance), "A private dinner, not a business one.")).IsSuccess);

        var line = Assert.Single(database.Logs.GetSnapshot(), record => record.Category == typeof(ApprovalDecisions).FullName);
        Assert.Equal(LogLevel.Information, line.Level);
        Assert.Equal($"Approval request {requestId.Value} of {database.FinanceKind.Key} is Rejected after step 1", line.Message);
        Assert.DoesNotContain("dinner", line.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_WhileAnotherDecisionHoldsTheRequest_IsRefusedAsBusy()
    {
        await using var database = await CreateAsync();
        var (asker, accountant) = (NewPerson(), NewPerson());
        var (claim, requestId) = await database.SubmittedClaimAsync(database.FinanceKind, asker);

        var busy = await database.WhileLockedAsync(requestId, () => database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(accountant, ErpRole.Finance)));

        Assert.Equal(("concurrency.conflict", ErrorKind.Conflict), (busy.Error!.Code, busy.Error.Kind));
        Assert.Equal(SampleClaimState.Waiting, (await database.ClaimAsync(claim)).State);
    }
}
