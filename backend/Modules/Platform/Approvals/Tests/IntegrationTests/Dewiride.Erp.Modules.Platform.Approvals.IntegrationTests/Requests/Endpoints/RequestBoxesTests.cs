using System.Net;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Shared;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Endpoints.Responses;
using Dewiride.Erp.Testing.Approvals;

namespace Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Requests.Endpoints;

// Each test reads a box or the counts of people of its own, and lists the requests of an API kind no other test takes.
public sealed class RequestBoxesTests(ApprovalsHost host) : IClassFixture<ApprovalsHost>
{
    [Fact]
    public async Task List_TheWaitingBox_ShowsTheManagerTheRequestsThatWaitForThemLateFirst()
    {
        var kind = TestApprovalKinds.Api(1);
        using var asker = await host.SignInAsync("Asha Rao");
        using var manager = await host.SignInAsync("Ravi Kumar");
        await host.ReportsToAsync(asker, manager);
        var recent = await host.SubmitAsync(kind, asker);
        var late = await host.SubmitAsync(kind, asker, submittedWorkingDaysAgo: 5);

        var byDefault = await ApprovalsApi.ListAsync(manager.Client, kind);
        var waiting = await ApprovalsApi.ListAsync(manager.Client, kind, "waiting");

        Assert.Equal([late, recent], byDefault.Items.Select(request => request.Id));
        Assert.Equal([late, recent], waiting.Items.Select(request => request.Id));
        Assert.Equal((true, false), (byDefault.Items[0].Late, byDefault.Items[1].Late));
        var first = byDefault.Items[0];
        Assert.Equal((kind.Key, kind.Title, TestApprovalKinds.SubjectType, (string?)null, ApprovalState.Submitted), (first.Kind, first.KindTitle, first.SubjectType, first.SubjectName, first.State));
        Assert.Equal((asker.ObjectId, asker.Name), (first.RequestedForId, first.RequestedForName!));
        Assert.Equal((ActorIds.System, (string?)null), (first.SubmittedById, first.SubmittedByName));
        Assert.Equal(TestApprovalKinds.ManagerStepTitle, first.WaitingStepTitle);
        Assert.Equal(((ApproverKind?)ApproverKind.Person, (Guid?)manager.ObjectId, manager.Name, (ErpRole?)null), (first.WaitingForKind, first.WaitingForId, first.WaitingForName!, first.WaitingForRole));
        Assert.NotNull(first.DueAt);
        Assert.Null(first.ClosedAt);
    }

    [Fact]
    public async Task List_TheMineBox_ShowsTheAskerTheirRequestsNewestFirstAndAManagerStepWithNoManagerWaitsForAdministrators()
    {
        var kind = TestApprovalKinds.Api(2);
        using var asker = await host.SignInAsync("Meera Nair");
        var older = await host.SubmitAsync(kind, asker, submittedWorkingDaysAgo: 1);
        var newer = await host.SubmitAsync(kind, asker);

        var mine = await ApprovalsApi.ListAsync(asker.Client, kind, "mine");

        Assert.Equal([newer, older], mine.Items.Select(request => request.Id));
        Assert.All(mine.Items, request => Assert.Equal(((ApproverKind?)ApproverKind.Role, (ErpRole?)ErpRole.Administrator, (Guid?)null), (request.WaitingForKind, request.WaitingForRole, request.WaitingForId)));
    }

    [Fact]
    public async Task List_TheDecidedBox_ShowsTheRequestsWithAStepTheCallerDecided()
    {
        var kind = TestApprovalKinds.Api(3);
        using var asker = await host.SignInAsync("Kiran Shah");
        using var manager = await host.SignInAsync("Sunita Pillai");
        await host.ReportsToAsync(asker, manager);
        var id = await host.SubmitAsync(kind, asker);
        await ApprovalsApi.ActAsync(manager.Client, id, "approve");

        var decided = await ApprovalsApi.ListAsync(manager.Client, kind, "decided");
        var waiting = await ApprovalsApi.ListAsync(manager.Client, kind, "waiting");

        var request = Assert.Single(decided.Items);
        Assert.Equal((id, ApprovalState.Submitted, TestApprovalKinds.FinanceStepTitle, (ErpRole?)ErpRole.Finance), (request.Id, request.State, request.WaitingStepTitle!, request.WaitingForRole));
        Assert.Empty(waiting.Items);
    }

    [Fact]
    public async Task List_TheAllBox_ShowsAnAdministratorEveryRequestAndRefusesAnEmployee()
    {
        var kind = TestApprovalKinds.Api(4);
        using var first = await host.SignInAsync("Kavya Menon");
        using var second = await host.SignInAsync("Arjun Das");
        var older = await host.SubmitAsync(kind, first, submittedWorkingDaysAgo: 1);
        var newer = await host.SubmitAsync(kind, second);
        using var administrator = host.Administrator();
        using var employee = host.Employee();

        var all = await ApprovalsApi.ListAsync(administrator, kind, "all");
        using var refused = await employee.GetAsync(ApprovalsApi.RequestsPath("?box=all"), TestContext.Current.CancellationToken);

        Assert.Equal([newer, older], all.Items.Select(request => request.Id));
        await ApprovalsApi.AssertProblemAsync(refused, HttpStatusCode.Forbidden, "request.forbidden");
    }

    [Fact]
    public async Task Count_AManagerAndAnAsker_CountWhatWaitsForEachAndWhatOfItIsLate()
    {
        var kind = TestApprovalKinds.Api(5);
        using var asker = await host.SignInAsync("Farah Khan");
        using var manager = await host.SignInAsync("Deepak Verma");
        await host.ReportsToAsync(asker, manager);
        await host.SubmitAsync(kind, asker);
        await host.SubmitAsync(kind, asker, submittedWorkingDaysAgo: 5);

        var forManager = await ApprovalsApi.CountAsync(manager.Client);
        var forAsker = await ApprovalsApi.CountAsync(asker.Client);

        Assert.Equal(new ApprovalCountsResponse(WaitingForMe: 2, LateForMe: 1, MineWaiting: 0), forManager);
        Assert.Equal(new ApprovalCountsResponse(WaitingForMe: 0, LateForMe: 0, MineWaiting: 2), forAsker);
    }

    [Fact]
    public async Task List_FilteredByTheRecordAndTheState_ShowsTheRequestsOfThatRecordAlone()
    {
        var kind = TestApprovalKinds.Api(6);
        using var asker = await host.SignInAsync("Ishaan Bose");
        var record = Guid.CreateVersion7();
        var withdrawn = await host.SubmitAsync(kind, asker, subjectId: record);
        await ApprovalsApi.ActAsync(asker.Client, withdrawn, "withdraw");
        var waiting = await host.SubmitAsync(kind, asker, subjectId: record);
        await host.SubmitAsync(kind, asker);

        var ofRecord = await ApprovalsApi.ListAsync(asker.Client, kind, "mine", $"subjectId:eq:{record}");
        var ofRecordWithdrawn = await ApprovalsApi.ListAsync(asker.Client, kind, "mine", $"subjectId:eq:{record};state:eq:withdrawn");

        Assert.Equal([waiting, withdrawn], ofRecord.Items.Select(request => request.Id));
        Assert.Equal([withdrawn], ofRecordWithdrawn.Items.Select(request => request.Id));
    }

    [Theory]
    [InlineData("everything")]
    [InlineData("Waiting")]
    [InlineData("all%0A")]
    public async Task List_ABoxTheListDoesNotKnow_IsRefusedKeyedByTheBox(string box)
    {
        using var employee = host.Employee();

        using var response = await employee.GetAsync(ApprovalsApi.RequestsPath($"?box={box}"), TestContext.Current.CancellationToken);

        var problem = await ApprovalsApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, "request.invalid");
        Assert.Equal(["box"], problem.Errors.Keys);
    }

    [Theory]
    [InlineData("?sort=kind:asc", "query.invalid-field")]
    [InlineData("?filter=subjectId%3Aeq%3Anot-a-guid", "query.invalid-value")]
    [InlineData("?filter=state%3Aeq%3Adecided", "query.invalid-value")]
    public async Task List_ATermTheListDoesNotAllow_IsRefused(string query, string code)
    {
        using var employee = host.Employee();

        using var response = await employee.GetAsync(ApprovalsApi.RequestsPath(query), TestContext.Current.CancellationToken);

        await ApprovalsApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, code);
    }
}
