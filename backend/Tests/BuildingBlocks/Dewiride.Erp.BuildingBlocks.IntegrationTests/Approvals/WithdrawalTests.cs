using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using static Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.ApprovalsDatabase;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals;

public sealed class WithdrawalTests
{
    [Fact]
    public async Task CancellingTheRecord_WithAWaitingRequest_WithdrawsItWithoutTheOutcomeHandler()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        var (claim, requestId) = await database.SubmittedClaimAsync(database.FinanceKind, asker);

        var cancelled = await database.CancelClaimAsync(database.FinanceKind, claim, asker);

        Assert.Equal(SampleClaimState.Cancelled, cancelled.Value);
        var request = await database.RequestAsync(requestId);
        Assert.Equal((ApprovalState.Withdrawn, (int?)null, (DateTimeOffset?)Today), (request.State, request.CurrentStep, request.ClosedAt));
        Assert.Equal((ApprovalStepState.Closed, (Guid?)null), (Assert.Single(request.Steps).State, request.Steps[0].DecidedBy));
        Assert.Empty(database.Script.Received);
    }

    [Fact]
    public async Task CancellingTheRecord_WithNothingWaiting_GoesThrough()
    {
        await using var database = await CreateAsync();
        var (asker, accountant) = (NewPerson(), NewPerson());
        var (claim, requestId) = await database.SubmittedClaimAsync(database.FinanceKind, asker);
        Assert.True((await database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(accountant, ErpRole.Finance))).IsSuccess);

        var cancelled = await database.CancelClaimAsync(database.FinanceKind, claim, asker);

        Assert.Equal(SampleClaimState.Cancelled, cancelled.Value);
        Assert.Equal(ApprovalState.Approved, (await database.RequestAsync(requestId)).State);
    }

    [Fact]
    public async Task WithdrawAsync_TwiceInOneUnitOfWork_WithdrawsTheRequestOnce()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        var (claim, requestId) = await database.SubmittedClaimAsync(database.FinanceKind, asker);

        var withdrawn = await database.RunUnitAsync<bool>(asker, async (context, approvals, token) =>
        {
            Assert.True((await approvals.WithdrawAsync(context, database.FinanceKind.Key, claim.Value, token)).IsSuccess);

            return Answer(await approvals.WithdrawAsync(context, database.FinanceKind.Key, claim.Value, token));
        });

        Assert.True(withdrawn.IsSuccess);
        Assert.Equal(ApprovalState.Withdrawn, (await database.RequestAsync(requestId)).State);
    }

    [Fact]
    public async Task WithdrawAsync_ARequestSubmittedEarlierInTheUnitOfWork_SavesItWithdrawn()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        var subject = Guid.CreateVersion7();

        var withdrawn = await database.RunUnitAsync<bool>(asker, async (context, approvals, token) =>
        {
            Assert.True((await approvals.SubmitAsync(context, database.FinanceKind.Key, subject, asker, token)).IsSuccess);

            return Answer(await approvals.WithdrawAsync(context, database.FinanceKind.Key, subject, token));
        });

        Assert.True(withdrawn.IsSuccess);
        var request = Assert.Single(await database.RequestsOfAsync(subject));
        Assert.Equal((ApprovalState.Withdrawn, ApprovalStepState.Closed), (request.State, Assert.Single(request.Steps).State));
    }

    [Fact]
    public async Task WithdrawAsync_AfterTheCommandReadItsRecord_Throws()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        var (claim, requestId) = await database.SubmittedClaimAsync(database.FinanceKind, asker);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => database.RunUnitAsync<bool>(asker, async (context, approvals, token) =>
        {
            await context.Claims.SingleAsync(c => c.Id == claim, token);

            return Answer(await approvals.WithdrawAsync(context, database.FinanceKind.Key, claim.Value, token));
        }));

        Assert.Contains("comes first in a command", thrown.Message, StringComparison.Ordinal);
        Assert.Equal(ApprovalState.Submitted, (await database.RequestAsync(requestId)).State);
    }

    [Fact]
    public async Task CancellingTheRecord_WhileADecisionOfItIsBeingSaved_WaitsForTheDecisionAndLeavesItStanding()
    {
        await using var database = await CreateAsync();
        var (asker, accountant) = (NewPerson(), NewPerson());
        var (claim, requestId) = await database.SubmittedClaimAsync(database.FinanceKind, asker);
        database.Script.Hold();

        var decision = database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(accountant, ErpRole.Finance));
        Assert.Same(database.Script.Reached, await Task.WhenAny(database.Script.Reached, decision).WaitAsync(TestContext.Current.CancellationToken));
        var askedForTheLock = database.LockRequests.NextRequest();
        var cancellation = database.CancelClaimAsync(database.FinanceKind, claim, asker);
        Assert.Same(askedForTheLock, await Task.WhenAny(askedForTheLock, cancellation).WaitAsync(TestContext.Current.CancellationToken));
        database.Script.Release();

        Assert.True((await decision).IsSuccess);
        Assert.Equal(SampleClaimState.Cancelled, (await cancellation).Value);
        Assert.Equal(ApprovalState.Approved, (await database.RequestAsync(requestId)).State);
        Assert.Equal(ApprovalState.Approved, Assert.Single(database.Script.Received).State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WithdrawAsync_OnThePageByThePersonItIsForOrWhoSubmittedIt_WithdrawsAndTellsTheModule(bool asker)
    {
        await using var database = await CreateAsync();
        var (person, submitter) = (NewPerson(), NewPerson());
        var (claim, requestId) = await database.SubmittedClaimAsync(database.ChainKind, person, submitter);
        var withdrawer = asker ? person : submitter;

        var withdrawn = await database.WithdrawAsync(requestId, Caller(withdrawer), " I booked the wrong date. ");

        Assert.Equal(ApprovalState.Withdrawn, withdrawn.Value.Request.State);
        Assert.Equal(("I booked the wrong date.", (Guid?)null), (withdrawn.Value.Steps[0].Comment, withdrawn.Value.Steps[0].DecidedById));
        Assert.All(withdrawn.Value.Steps, step => Assert.Equal(ApprovalStepState.Closed, step.State));
        Assert.Equal(SampleClaimState.Draft, (await database.ClaimAsync(claim)).State);
        var outcome = Assert.Single(database.Script.Received);
        Assert.Equal((ApprovalState.Withdrawn, withdrawer, "I booked the wrong date."), (outcome.State, outcome.DecidedBy, outcome.Comment));
    }

    [Fact]
    public async Task WithdrawAsync_OnThePageBySomebodyElseWhoSeesTheRequest_IsRefused()
    {
        await using var database = await CreateAsync();
        var (asker, manager) = (NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (claim, requestId) = await database.SubmittedClaimAsync(database.ManagerKind, asker);

        var byTheManager = await database.WithdrawAsync(requestId, Caller(manager));
        var byAnAdministrator = await database.WithdrawAsync(requestId, Administrator(NewPerson()));
        var byAStranger = await database.WithdrawAsync(requestId, Caller(NewPerson()));

        Assert.Equal(("approval.not-yours-to-withdraw", ErrorKind.Forbidden), (byTheManager.Error!.Code, byTheManager.Error.Kind));
        Assert.Equal("approval.not-yours-to-withdraw", byAnAdministrator.Error!.Code);
        Assert.Equal(ApprovalErrors.RequestNotFound, byAStranger.Error);
        Assert.Equal(SampleClaimState.Waiting, (await database.ClaimAsync(claim)).State);
    }

    [Fact]
    public async Task WithdrawAsync_OnThePageOfARequestNoLongerWaiting_IsRefused()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        var (_, requestId) = await database.SubmittedClaimAsync(database.FinanceKind, asker);
        Assert.True((await database.WithdrawAsync(requestId, Caller(asker))).IsSuccess);

        var again = await database.WithdrawAsync(requestId, Caller(asker));

        Assert.Equal(("approval.not-waiting", ErrorKind.Conflict), (again.Error!.Code, again.Error.Kind));
    }

    private static Result<bool> Answer(Result result) => result.IsSuccess ? true : result.Error!;
}
