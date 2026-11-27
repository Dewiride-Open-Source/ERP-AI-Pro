using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using static Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.ApprovalsDatabase;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals;

public sealed class ConcurrentDecisionTests
{
    private const string RequestsKey = "platform.approvals.requests";

    private const string StepsKey = "platform.approvals.steps";

    [Fact]
    public async Task DecideAsync_TenPeopleDecidingOneRequestAtOnce_LeaveExactlyOneDecision()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        var (claim, requestId) = await database.SubmittedClaimAsync(database.FinanceKind, asker);
        var version = await database.VersionOfAsync(requestId);
        var deciders = Enumerable.Range(0, 10).Select(_ => NewPerson()).ToList();

        var outcomes = await Task.WhenAll(deciders.Select((decider, index) =>
            database.DecideAsync(requestId, index % 2 == 0 ? ApprovalDecision.Approve : ApprovalDecision.Reject, Caller(decider, ErpRole.Finance), "Decided at once.", version)));

        var decided = Assert.Single(outcomes, outcome => outcome.IsSuccess);
        Assert.All(outcomes.Where(outcome => outcome.IsFailure), outcome => Assert.Equal(ErrorKind.Conflict, outcome.Error!.Kind));
        var request = await database.RequestAsync(requestId);
        var step = Assert.Single(request.Steps);
        Assert.Equal(decided.Value.Request.State, request.State);
        Assert.Contains(step.DecidedBy!.Value, deciders);
        Assert.Single(database.Script.Received);
        var claimState = (await database.ClaimAsync(claim)).State;
        Assert.Equal(request.State == ApprovalState.Approved ? SampleClaimState.Approved : SampleClaimState.Rejected, claimState);
    }

    [Fact]
    public async Task DecideAsync_ADecision_IsRecordedForTheRequestItsStepAndTheRecordAsTheDeciderUnderOneCorrelationId()
    {
        await using var database = await CreateAsync();
        var (asker, submitter, accountant) = (NewPerson(), NewPerson(), NewPerson());
        var (claim, requestId) = await database.SubmittedClaimAsync(database.FinanceKind, asker, submitter);
        var stepId = (await database.RequestAsync(requestId)).Steps[0].Id;
        database.Clock.Advance(TimeSpan.FromHours(1));

        Assert.True((await database.DecideAsync(requestId, ApprovalDecision.Approve, Caller(accountant, ErpRole.Finance))).IsSuccess);

        var requestRecords = await database.ChangeRecordsOfAsync(requestId.Value);
        var stepRecords = await database.ChangeRecordsOfAsync(stepId.Value);
        var claimRecords = await database.ChangeRecordsOfAsync(claim.Value);
        Assert.Equal([(ChangeOperation.Created, submitter), (ChangeOperation.Updated, accountant)], requestRecords.Select(record => (record.Operation, record.ActorId)));
        Assert.Equal([(ChangeOperation.Created, submitter), (ChangeOperation.Updated, accountant)], stepRecords.Select(record => (record.Operation, record.ActorId)));
        Assert.Equal([(ChangeOperation.Created, submitter), (ChangeOperation.Updated, accountant)], claimRecords.Select(record => (record.Operation, record.ActorId)));
        Assert.All(requestRecords, record => Assert.Equal(RequestsKey, record.EntityType));
        Assert.All(stepRecords, record => Assert.Equal((StepsKey, RequestsKey, requestId.Value), (record.EntityType, record.SubjectType, record.SubjectId)));
        var decision = new[] { requestRecords[1], stepRecords[1], claimRecords[1] };
        Assert.Single(decision.Select(record => record.CorrelationId).Distinct());
        Assert.NotEqual(requestRecords[0].CorrelationId, requestRecords[1].CorrelationId);
        Assert.All(decision, record => Assert.Equal(Today.AddHours(1), record.OccurredAt));
    }
}
