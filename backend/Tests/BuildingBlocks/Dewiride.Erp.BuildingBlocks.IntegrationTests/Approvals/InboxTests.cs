using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using static Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.ApprovalsDatabase;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals;

// Every list but a person's own is filtered by the fixture's kinds, because the requests that wait for a role are every test's.
public sealed class InboxTests
{
    [Fact]
    public async Task ListAsync_Waiting_HoldsTheRequestsThatWaitForTheCallerLateFirstAndMarksTheLateOnes()
    {
        await using var database = await CreateAsync();
        var (manager, otherManager) = (NewPerson(), NewPerson());
        var requests = new List<ApprovalRequestId>();
        database.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 30, 4, 30, 0, TimeSpan.Zero));
        for (var day = 0; day < 3; day++)
        {
            if (day > 0)
            {
                database.Clock.Advance(TimeSpan.FromDays(1));
            }

            var asker = NewPerson();
            database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
            requests.Add((await database.SubmittedClaimAsync(database.ManagerKind, asker)).Request);
        }

        var elsewhere = NewPerson();
        database.Managers.Set(elsewhere, new ReportingManager(otherManager, IsActive: true));
        await database.SubmittedClaimAsync(database.ManagerKind, elsewhere);
        database.Clock.SetUtcNow(new DateTimeOffset(2026, 12, 3, 4, 30, 0, TimeSpan.Zero));

        var waiting = await ListAsync(database, ApprovalBox.Waiting, Caller(manager));

        Assert.Equal(requests, waiting.Items.Select(summary => summary.Id));
        Assert.Equal([true, false, false], waiting.Items.Select(summary => summary.Late));
        Assert.All(waiting.Items, summary => Assert.Equal((ApproverKind?)ApproverKind.Person, summary.WaitingForKind));
        Assert.Equal(3, waiting.TotalCount);
    }

    [Fact]
    public async Task ListAsync_Waiting_HoldsWhatWaitsForTheCallersRolesButNeverTheirOwnRequests()
    {
        await using var database = await CreateAsync();
        var (asker, accountant) = (NewPerson(), NewPerson());
        var (_, forFinance) = await database.SubmittedClaimAsync(database.FinanceKind, asker);
        var (_, ownRequest) = await database.SubmittedClaimAsync(database.FinanceKind, accountant);
        var (_, submittedForSomebody) = await database.SubmittedClaimAsync(database.FinanceKind, NewPerson(), accountant);

        var financeHolder = await ListAsync(database, ApprovalBox.Waiting, Caller(accountant, ErpRole.Finance), KindIs(database.FinanceKind.Key));
        var somebodyElse = await ListAsync(database, ApprovalBox.Waiting, Caller(NewPerson(), ErpRole.Hr), KindIs(database.FinanceKind.Key));

        Assert.Equal([forFinance], financeHolder.Items.Select(summary => summary.Id));
        Assert.Equal((ApproverKind?)ApproverKind.Role, financeHolder.Items[0].WaitingForKind);
        Assert.Equal((ErpRole?)ErpRole.Finance, financeHolder.Items[0].WaitingForRole);
        Assert.Empty(somebodyElse.Items);
        Assert.DoesNotContain(ownRequest, financeHolder.Items.Select(summary => summary.Id));
        Assert.DoesNotContain(submittedForSomebody, financeHolder.Items.Select(summary => summary.Id));
    }

    [Fact]
    public async Task ListAsync_Mine_HoldsWhatTheCallerAskedForOrSubmittedNewestFirst()
    {
        await using var database = await CreateAsync();
        var (person, colleague) = (NewPerson(), NewPerson());
        var (_, asked) = await database.SubmittedClaimAsync(database.FinanceKind, person);
        database.Clock.Advance(TimeSpan.FromMinutes(5));
        var (_, submitted) = await database.SubmittedClaimAsync(database.FinanceKind, colleague, person);
        database.Clock.Advance(TimeSpan.FromMinutes(5));
        await database.SubmittedClaimAsync(database.FinanceKind, colleague);

        var mine = await ListAsync(database, ApprovalBox.Mine, Caller(person));

        Assert.Equal([submitted, asked], mine.Items.Select(summary => summary.Id));
    }

    [Fact]
    public async Task ListAsync_Decided_HoldsTheRequestsWithAStepTheCallerDecided()
    {
        await using var database = await CreateAsync();
        var (asker, manager) = (NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (_, decided) = await database.SubmittedClaimAsync(database.ChainKind, asker);
        await database.SubmittedClaimAsync(database.ChainKind, asker);
        Assert.True((await database.DecideAsync(decided, ApprovalDecision.Approve, Caller(manager))).IsSuccess);

        var list = await ListAsync(database, ApprovalBox.Decided, Caller(manager));

        var summary = Assert.Single(list.Items);
        Assert.Equal((decided, ApprovalState.Submitted, "Finance", (ErpRole?)ErpRole.Finance), (summary.Id, summary.State, summary.WaitingStepTitle, summary.WaitingForRole));
    }

    [Fact]
    public async Task ListAsync_All_IsForAPersonWhoSeesEveryRequestAlone()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        var (_, first) = await database.SubmittedClaimAsync(database.FinanceKind, asker);
        database.Clock.Advance(TimeSpan.FromMinutes(1));
        var (_, second) = await database.SubmittedClaimAsync(database.ManagerKind, asker);

        var refused = await database.InboxAsync(Caller(asker, ErpRole.Finance), (inbox, token) => inbox.ListAsync(ApprovalBox.All, ListRequest.Default, Caller(asker, ErpRole.Finance), token));
        var all = await ListAsync(database, ApprovalBox.All, Administrator(NewPerson()), $"kind:in:{database.FinanceKind.Key}|{database.ManagerKind.Key}");

        Assert.Equal(("request.forbidden", ErrorKind.Forbidden), (refused.Error!.Code, refused.Error.Kind));
        Assert.Equal([second, first], all.Items.Select(summary => summary.Id));
    }

    [Fact]
    public async Task ListAsync_FilteredByRecordAndState_HoldsTheMatchingRequests()
    {
        await using var database = await CreateAsync();
        var (asker, accountant) = (NewPerson(), NewPerson());
        var (claim, first) = await database.SubmittedClaimAsync(database.FinanceKind, asker);
        Assert.True((await database.DecideAsync(first, ApprovalDecision.Return, Caller(accountant, ErpRole.Finance), "Add the receipt.")).IsSuccess);
        database.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = (await database.ResubmitClaimAsync(database.FinanceKind, claim, asker)).Value.RequestId!.Value;
        await database.SubmittedClaimAsync(database.FinanceKind, asker);

        var ofTheClaim = await ListAsync(database, ApprovalBox.Mine, Caller(asker), $"subjectId:eq:{claim.Value}");
        var returned = await ListAsync(database, ApprovalBox.Mine, Caller(asker), $"subjectId:eq:{claim.Value};state:eq:returned");

        Assert.Equal([second, first], ofTheClaim.Items.Select(summary => summary.Id));
        Assert.Equal([first], returned.Items.Select(summary => summary.Id));
    }

    [Theory]
    [InlineData("amount:asc", null)]
    [InlineData(null, "requestedFor:eq:01999a03-0000-7000-8000-000000000001")]
    public async Task ListAsync_ASortOrFilterOutsideTheAllowLists_IsRefused(string? sort, string? filter)
    {
        await using var database = await CreateAsync();
        var caller = Caller(NewPerson());

        var refused = await database.InboxAsync(caller, (inbox, token) => inbox.ListAsync(ApprovalBox.Mine, ListRequest.Parse(null, null, sort, filter).Value, caller, token));

        Assert.Equal(QueryErrors.InvalidField, refused.Error!.Code);
    }

    [Fact]
    public async Task ListAsync_TheNames_ComeFromThePeopleAndTheRecordsTheirModulesName()
    {
        await using var database = await CreateAsync();
        var (asker, submitter, manager) = (NewPerson(), NewPerson(), NewPerson());
        database.People.Name(asker, "Asha Rao");
        database.People.Name(submitter, "Vikram Shah");
        database.People.Name(manager, "Meera Iyer");
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (claim, _) = await database.SubmitClaimAsync(database.ManagerKind, asker, submitter, title: "Flight to Chennai");

        var summary = Assert.Single((await ListAsync(database, ApprovalBox.Waiting, Caller(manager))).Items);

        Assert.Equal(
            (database.ManagerKind.Title, claim.Value, "Flight to Chennai", "Asha Rao", "Vikram Shah", "Manager", "Meera Iyer"),
            (summary.KindTitle, summary.SubjectId, summary.SubjectName, summary.RequestedForName, summary.SubmittedByName, summary.WaitingStepTitle, summary.WaitingForName));
    }

    [Fact]
    public async Task GetAsync_ARequest_ShowsItsStepsItsVersionItsEarlierRequestsAndWhatTheCallerMayDo()
    {
        await using var database = await CreateAsync();
        var (asker, manager) = (NewPerson(), NewPerson());
        database.People.Name(manager, "Meera Iyer");
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (claim, first) = await database.SubmittedClaimAsync(database.ChainKind, asker);
        Assert.True((await database.DecideAsync(first, ApprovalDecision.Return, Caller(manager), "Split the hotel and the meals.")).IsSuccess);
        database.Clock.Advance(TimeSpan.FromHours(2));
        var second = (await database.ResubmitClaimAsync(database.ChainKind, claim, asker)).Value.RequestId!.Value;

        var stranger = Caller(NewPerson(), ErpRole.Hr);

        var forTheManager = await GetAsync(database, second, Caller(manager));
        var forTheAsker = await GetAsync(database, second, Caller(asker));
        var forAStranger = await database.InboxAsync(stranger, (inbox, token) => inbox.GetAsync(second, stranger, token));

        Assert.Equal((true, false, false), (forTheManager.CanDecide, forTheManager.DecidesInPlace, forTheManager.CanWithdraw));
        Assert.Equal((false, false, true), (forTheAsker.CanDecide, forTheAsker.DecidesInPlace, forTheAsker.CanWithdraw));
        Assert.Equal(Convert.ToBase64String(await database.VersionOfAsync(second)), forTheManager.Version);
        Assert.Equal(
            [(1, "Manager", ApprovalStepState.Waiting, "Meera Iyer", (ErpRole?)null), (2, "Finance", ApprovalStepState.Pending, null, ErpRole.Finance)],
            forTheManager.Steps.Select(step => (step.Order, step.Title, step.State, step.ApproverName, step.ApproverRole)));
        var earlier = Assert.Single(forTheManager.EarlierRequests);
        Assert.Equal((first, ApprovalState.Returned, Today), (earlier.Id, earlier.State, earlier.SubmittedAt));
        Assert.Equal(ApprovalErrors.RequestNotFound, forAStranger.Error);
    }

    [Fact]
    public async Task GetAsync_ARecordAskedForThreeTimes_ListsItsEarlierRequestsNewestFirstToThoseWhoTookPartAlone()
    {
        await using var database = await CreateAsync();
        var (asker, manager, newManager) = (NewPerson(), NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        var (claim, first) = await database.SubmittedClaimAsync(database.ManagerKind, asker);
        Assert.True((await database.DecideAsync(first, ApprovalDecision.Return, Caller(manager), "Add the receipt.")).IsSuccess);
        database.Clock.Advance(TimeSpan.FromHours(1));
        var second = (await database.ResubmitClaimAsync(database.ManagerKind, claim, asker)).Value.RequestId!.Value;
        Assert.True((await database.DecideAsync(second, ApprovalDecision.Return, Caller(manager), "Split the hotel and the meals.")).IsSuccess);
        database.Managers.Set(asker, new ReportingManager(newManager, IsActive: true));
        database.Clock.Advance(TimeSpan.FromHours(1));
        var third = (await database.ResubmitClaimAsync(database.ManagerKind, claim, asker)).Value.RequestId!.Value;

        var forTheAsker = await GetAsync(database, third, Caller(asker));
        var forTheNewManager = await GetAsync(database, third, Caller(newManager));
        var forAnAdministrator = await GetAsync(database, third, Administrator(NewPerson()));

        Assert.Equal([second, first], forTheAsker.EarlierRequests.Select(earlier => earlier.Id));
        Assert.Empty(forTheNewManager.EarlierRequests);
        Assert.Equal([second, first], forAnAdministrator.EarlierRequests.Select(earlier => earlier.Id));
    }

    [Fact]
    public async Task GetAsync_ARecordAskedForTwelveTimes_ListsItsTenNewestEarlierRequests()
    {
        await using var database = await CreateAsync();
        var asker = NewPerson();
        var (claim, request) = await database.SubmittedClaimAsync(database.FinanceKind, asker);
        var asked = new List<ApprovalRequestId> { request };
        for (var again = 0; again < 11; again++)
        {
            Assert.True((await database.WithdrawAsync(request, Caller(asker))).IsSuccess);
            database.Clock.Advance(TimeSpan.FromMinutes(1));
            request = (await database.ResubmitClaimAsync(database.FinanceKind, claim, asker)).Value.RequestId!.Value;
            asked.Add(request);
        }

        var details = await GetAsync(database, request, Caller(asker));

        Assert.Equal(asked[1..^1].AsEnumerable().Reverse(), details.EarlierRequests.Select(earlier => earlier.Id));
    }

    [Fact]
    public async Task CountAsync_ForTheCaller_CountsWhatWaitsForThemWhatOfThatIsLateAndTheirOwnStillWaiting()
    {
        await using var database = await CreateAsync();
        var (manager, asker) = (NewPerson(), NewPerson());
        database.Managers.Set(asker, new ReportingManager(manager, IsActive: true));
        await database.SubmittedClaimAsync(database.ManagerKind, asker);
        database.Clock.Advance(TimeSpan.FromDays(5));
        await database.SubmittedClaimAsync(database.ManagerKind, asker);
        var (_, decided) = await database.SubmittedClaimAsync(database.ManagerKind, asker);
        Assert.True((await database.DecideAsync(decided, ApprovalDecision.Approve, Caller(manager))).IsSuccess);

        var forTheManager = await database.InboxAsync(Caller(manager), (inbox, token) => inbox.CountAsync(Caller(manager), token));
        var forTheAsker = await database.InboxAsync(Caller(asker), (inbox, token) => inbox.CountAsync(Caller(asker), token));

        Assert.Equal(new ApprovalCounts(WaitingForMe: 2, LateForMe: 1, MineWaiting: 0), forTheManager);
        Assert.Equal(new ApprovalCounts(WaitingForMe: 0, LateForMe: 0, MineWaiting: 2), forTheAsker);
    }

    private static string KindIs(string kind) => $"kind:eq:{kind}";

    private static async Task<PagedResult<ApprovalRequestSummary>> ListAsync(ApprovalsDatabase database, ApprovalBox box, ApprovalCaller caller, string? filter = null)
    {
        var list = ListRequest.Parse(page: null, pageSize: null, sort: null, filter).Value;
        var answer = await database.InboxAsync(caller, (inbox, token) => inbox.ListAsync(box, list, caller, token));

        return answer.Value;
    }

    private static async Task<ApprovalRequestDetails> GetAsync(ApprovalsDatabase database, ApprovalRequestId id, ApprovalCaller caller) =>
        (await database.InboxAsync(caller, (inbox, token) => inbox.GetAsync(id, caller, token))).Value;
}
