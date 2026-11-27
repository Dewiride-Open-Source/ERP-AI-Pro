using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Persistence;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Approvals.Submitting;
using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using static Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.ApprovalsDatabase;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals;

public sealed class SubmissionTests
{
    private static readonly DateTimeOffset DueOnWednesday = IndianStandardTime.StartOfIstDay(new DateOnly(2026, 12, 2));

    [Fact]
    public async Task SubmitAsync_InTheCommandThatSendsTheRecord_CommitsTheRequestWithTheRecord()
    {
        await using var database = await CreateAsync();
        var (asker, submitter, manager) = (NewPerson(), NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));

        var (claim, submitted) = await database.SubmitClaimAsync(database.ManagerKind, asker, submitter);

        Assert.Equal(ApprovalState.Submitted, submitted.Value.State);
        var request = await database.RequestAsync(submitted.Value.RequestId!.Value);
        Assert.Equal((database.ManagerKind.Key, SampleClaimConfiguration.ChangeKey, claim.Value, asker, submitter), (request.Kind, request.SubjectType, request.SubjectId, request.RequestedFor, request.CreatedBy));
        Assert.Equal((ApprovalState.Submitted, (int?)1, Today, (DateTimeOffset?)null), (request.State, request.CurrentStep, request.SubmittedAt, request.ClosedAt));
        var step = Assert.Single(request.Steps);
        Assert.Equal(("Manager", ApprovalStepRule.Manager, ApprovalStepState.Waiting, ApproverKind.Person, (Guid?)manager), (step.Title, step.Rule, step.State, step.ApproverKind, step.ApproverId));
        Assert.Equal((KindWorkingDays, (DateTimeOffset?)Today, (DateTimeOffset?)DueOnWednesday), (step.WorkingDays, step.StartedAt, step.DueAt));
        Assert.Equal(SampleClaimState.Waiting, (await database.ClaimAsync(claim)).State);
    }

    [Fact]
    public async Task SubmitAsync_InAUnitOfWorkThatFailsAfterwards_LeavesNeitherTheRecordNorTheRequest()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        var claim = SampleClaimId.Create();

        var failed = await database.RunUnitAsync<string>(asker, async (context, approvals, token) =>
        {
            context.Claims.Add(SampleClaim.Draft(claim, "Hotel in Pune", asker, receiptAttached: true));
            Assert.True((await approvals.SubmitAsync(context, database.ManagerKind.Key, claim.Value, asker, token)).IsSuccess);

            return Error.Failure("test.approvals.refused-later", "The command refuses once its approval is asked for.");
        });

        Assert.Equal("test.approvals.refused-later", failed.Error!.Code);
        Assert.Empty(await database.RequestsOfAsync(claim.Value));
        Assert.Null(await database.FindClaimAsync(claim));
    }

    [Fact]
    public async Task SubmitAsync_ARecordThatAlreadyWaits_IsRefusedWhetherTheFirstIsSavedOrNot()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        var (claim, _) = await database.SubmittedClaimAsync(database.ManagerKind, asker);
        var subject = Guid.CreateVersion7();

        var saved = await database.RunUnitAsync<ApprovalSubmitted>(asker, (context, approvals, token) => approvals.SubmitAsync(context, database.ManagerKind.Key, claim.Value, asker, token));
        var unsaved = await database.RunUnitAsync<ApprovalSubmitted>(asker, async (context, approvals, token) =>
        {
            Assert.True((await approvals.SubmitAsync(context, database.ManagerKind.Key, subject, asker, token)).IsSuccess);

            return await approvals.SubmitAsync(context, database.ManagerKind.Key, subject, asker, token);
        });

        Assert.Equal(("approval.already-submitted", ErrorKind.Conflict), (saved.Error!.Code, saved.Error.Kind));
        Assert.Equal("approval.already-submitted", unsaved.Error!.Code);
        Assert.Single(await database.RequestsOfAsync(claim.Value));
        Assert.Empty(await database.RequestsOfAsync(subject));
    }

    [Fact]
    public async Task SubmitAsync_ManySubmissionsOfOneRecordAtOnce_LeaveOneWaitingRequest()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        var subject = Guid.CreateVersion7();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            database.RunUnitAsync<ApprovalSubmitted>(asker, (context, approvals, token) => approvals.SubmitAsync(context, database.FinanceKind.Key, subject, asker, token))));

        Assert.Single(outcomes, outcome => outcome.IsSuccess);
        Assert.All(outcomes.Where(outcome => outcome.IsFailure), outcome => Assert.Equal(ErrorKind.Conflict, outcome.Error!.Kind));
        Assert.Equal(ApprovalState.Submitted, Assert.Single(await database.RequestsOfAsync(subject)).State);
    }

    [Fact]
    public async Task SubmitAsync_AManagerStep_AsksForTheAskersManagersOnTheDayOfSubmissionInIndia()
    {
        await using var database = await CreateAsync();
        var (asker, manager) = (NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        database.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 27, 19, 0, 0, TimeSpan.Zero));

        await database.SubmittedClaimAsync(database.ManagerKind, asker);

        Assert.Equal([(asker, new DateOnly(2026, 11, 28))], database.Managers.Asked);
    }

    [Fact]
    public async Task SubmitAsync_TheAskersManagerWhoSubmitsForThem_IsPassedOverForTheManagerAbove()
    {
        await using var database = await CreateAsync();
        var (asker, submittingManager, above) = (NewPerson(), NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(submittingManager, IsActive: true), new ReportingManager(above, IsActive: true));

        var (_, requestId) = await database.SubmittedClaimAsync(database.ManagerKind, asker, submittingManager);

        Assert.Equal(above, Assert.Single((await database.RequestAsync(requestId)).Steps).ApproverId);
    }

    [Fact]
    public async Task SubmitAsync_NoManagerLeftToDecide_WaitsForTheAdministrators()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        database.Managers.Set(asker, new ReportingManager(NewPerson(), IsActive: false), new ReportingManager(null, IsActive: true));

        var (_, requestId) = await database.SubmittedClaimAsync(database.ManagerKind, asker);

        var step = Assert.Single((await database.RequestAsync(requestId)).Steps);
        Assert.Equal((ApprovalStepRule.Manager, ApproverKind.Role, (Guid?)null, (ErpRole?)ErpRole.Administrator), (step.Rule, step.ApproverKind, step.ApproverId, step.ApproverRole));
    }

    [Fact]
    public async Task SubmitAsync_ManagerThenFinance_SettlesBothStepsAtSubmission()
    {
        await using var database = await CreateAsync();
        var (asker, manager) = (NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));

        var (_, requestId) = await database.SubmittedClaimAsync(database.ChainKind, asker);

        var steps = (await database.RequestAsync(requestId)).Steps.OrderBy(step => step.Order).ToList();
        Assert.Equal(
            [
                (1, ApprovalStepState.Waiting, ApproverKind.Person, (Guid?)manager, (ErpRole?)null, (DateTimeOffset?)DueOnWednesday),
                (2, ApprovalStepState.Pending, ApproverKind.Role, null, ErpRole.Finance, null),
            ],
            steps.Select(step => (step.Order, step.State, step.ApproverKind, step.ApproverId, step.ApproverRole, step.DueAt)));
    }

    [Fact]
    public async Task SubmitAsync_WhileAdministratorsSwitchedTheKindOff_ApprovesTheRecordWithoutARequest()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        Assert.True((await database.AdministerAsync(NewPerson(), (administration, token) => administration.SetUpAsync(database.ManagerKind.Key, required: false, KindWorkingDays, token))).IsSuccess);

        var (claim, submitted) = await database.SubmitClaimAsync(database.ManagerKind, asker);

        Assert.Equal(new ApprovalSubmitted(null, ApprovalState.Approved), submitted.Value);
        Assert.Empty(await database.RequestsOfAsync(claim.Value));
        Assert.Equal(SampleClaimState.Approved, (await database.ClaimAsync(claim)).State);
        Assert.Empty(database.Managers.Asked);
    }

    [Fact]
    public async Task SubmitAsync_AKindsSettingOfWorkingDays_GivesEveryStepOfTheRequestThoseDays()
    {
        await using var database = await CreateAsync();
        var (asker, manager) = (NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        Assert.True((await database.AdministerAsync(NewPerson(), (administration, token) => administration.SetUpAsync(database.ChainKind.Key, required: true, 5, token))).IsSuccess);

        var (_, requestId) = await database.SubmittedClaimAsync(database.ChainKind, asker);

        var request = await database.RequestAsync(requestId);
        Assert.All(request.Steps, step => Assert.Equal(5, step.WorkingDays));
        Assert.Equal(IndianStandardTime.StartOfIstDay(new DateOnly(2026, 12, 5)), request.Steps.Single(step => step.Order == 1).DueAt);
    }

    [Fact]
    public async Task SubmitAsync_AfterWithdrawingTheRecordsRequestInTheSameUnitOfWork_Throws()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        var (claim, requestId) = await database.SubmittedClaimAsync(database.FinanceKind, asker);

        await Assert.ThrowsAsync<InvalidOperationException>(() => database.RunUnitAsync<ApprovalSubmitted>(asker, async (context, approvals, token) =>
        {
            Assert.True((await approvals.WithdrawAsync(context, database.FinanceKind.Key, claim.Value, token)).IsSuccess);

            return await approvals.SubmitAsync(context, database.FinanceKind.Key, claim.Value, asker, token);
        }));

        Assert.Equal(ApprovalState.Submitted, (await database.RequestAsync(requestId)).State);
    }

    [Fact]
    public async Task SubmitAsync_ARecordWithdrawnInAnEarlierUnitOfWork_AsksInANewRequestBesideTheOld()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        var (claim, first) = await database.SubmittedClaimAsync(database.FinanceKind, asker);
        Assert.True((await database.RunUnitAsync<bool>(asker, async (context, approvals, token) =>
        {
            var withdrawn = await approvals.WithdrawAsync(context, database.FinanceKind.Key, claim.Value, token);

            return withdrawn.IsSuccess ? true : withdrawn.Error!;
        })).IsSuccess);
        database.Clock.Advance(TimeSpan.FromMinutes(1));

        var again = await database.RunUnitAsync<ApprovalSubmitted>(asker, (context, approvals, token) => approvals.SubmitAsync(context, database.FinanceKind.Key, claim.Value, asker, token));

        Assert.NotEqual(first, again.Value.RequestId);
        Assert.Equal([ApprovalState.Withdrawn, ApprovalState.Submitted], (await database.RequestsOfAsync(claim.Value)).Select(request => request.State));
    }

    [Fact]
    public async Task SubmitAsync_AKindNoModuleDeclares_Throws()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();

        await Assert.ThrowsAsync<InvalidOperationException>(() => database.RunUnitAsync<ApprovalSubmitted>(asker, (context, approvals, token) => approvals.SubmitAsync(context, "test.approvals.undeclared", Guid.CreateVersion7(), asker, token)));
    }

    [Fact]
    public async Task SubmitAsync_OnAnotherContextThanTheKinds_Throws()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => database.AsAsync(asker, (services, token) =>
            services.GetRequiredService<IApprovals>().SubmitAsync(services.GetRequiredService<ApprovalsDbContext>(), database.ManagerKind.Key, Guid.CreateVersion7(), asker, token)));

        Assert.Contains(nameof(SampleDbContext), thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitAsync_OnAContextThatMapsNoApprovals_Throws()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => database.AsAsync(asker, (services, token) =>
            services.GetRequiredService<IApprovals>().SubmitAsync(services.GetRequiredService<AuditingDbContext>(), database.UnmappedKind.Key, Guid.CreateVersion7(), asker, token)));

        Assert.Contains("maps no approvals", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitAsync_OutsideAUnitOfWork_Throws()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => database.AsAsync(asker, (services, token) =>
            services.GetRequiredService<IApprovals>().SubmitAsync(services.GetRequiredService<SampleDbContext>(), database.ManagerKind.Key, Guid.CreateVersion7(), asker, token)));

        Assert.Contains("outside a unit of work", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitAsync_ForSomebodyWhoIsNotAPerson_Throws()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();

        foreach (var nobody in new[] { Guid.Empty, ActorIds.System, ActorIds.Anonymous })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => database.RunUnitAsync<ApprovalSubmitted>(asker, (context, approvals, token) => approvals.SubmitAsync(context, database.ManagerKind.Key, Guid.CreateVersion7(), nobody, token)));
        }
    }

    [Fact]
    public async Task StatusOfAsync_ARecordsRequests_AnswersTheLatestAndNothingForARecordWithout()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        var (claim, requestId) = await database.SubmittedClaimAsync(database.FinanceKind, asker);

        var status = await database.AsAsync(asker, (services, token) => services.GetRequiredService<IApprovals>().StatusOfAsync(services.GetRequiredService<SampleDbContext>(), database.FinanceKind.Key, claim.Value, token));
        var none = await database.AsAsync(asker, (services, token) => services.GetRequiredService<IApprovals>().StatusOfAsync(services.GetRequiredService<SampleDbContext>(), database.FinanceKind.Key, Guid.CreateVersion7(), token));

        Assert.Equal(new ApprovalStatus(requestId, ApprovalState.Submitted, 1, "Finance", DueOnWednesday), status);
        Assert.Null(none);
    }

    [Fact]
    public async Task StatusOfAsync_ARecordAskedForAgain_AnswersItsNewestRequestThenThatRequestClosed()
    {
        await using var database = await CreateAsync();
        var (asker, accountant) = (NewPerson(), NewPerson());
        var (claim, first) = await database.SubmittedClaimAsync(database.FinanceKind, asker);
        Assert.True((await database.WithdrawAsync(first, Caller(asker))).IsSuccess);
        database.Clock.Advance(TimeSpan.FromHours(1));
        var second = (await database.ResubmitClaimAsync(database.FinanceKind, claim, asker)).Value.RequestId!.Value;

        var waiting = await StatusOfAsync(database, asker, claim);
        Assert.True((await database.DecideAsync(second, ApprovalDecision.Approve, Caller(accountant, ErpRole.Finance))).IsSuccess);
        var approved = await StatusOfAsync(database, asker, claim);

        Assert.Equal(new ApprovalStatus(second, ApprovalState.Submitted, 1, "Finance", DueOnWednesday), waiting);
        Assert.Equal(new ApprovalStatus(second, ApprovalState.Approved, null, null, null), approved);
    }

    private static Task<ApprovalStatus?> StatusOfAsync(ApprovalsDatabase database, Guid actor, SampleClaimId claim) =>
        database.AsAsync(actor, (services, token) => services.GetRequiredService<IApprovals>().StatusOfAsync(services.GetRequiredService<SampleDbContext>(), database.FinanceKind.Key, claim.Value, token));
}
