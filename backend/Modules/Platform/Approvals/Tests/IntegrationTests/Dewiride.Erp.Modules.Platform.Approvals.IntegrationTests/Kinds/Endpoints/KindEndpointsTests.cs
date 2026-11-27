using System.Net;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Submitting;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Shared;
using Dewiride.Erp.Modules.Platform.Approvals.Kinds.Endpoints.Responses;
using Dewiride.Erp.Testing.Approvals;

namespace Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Kinds.Endpoints;

// A setting is its kind's for every test of the process, so each test that sets a kind up takes an API kind no other test takes, and
// the set-ups refused before they reach a setting share the last one, which no test sets up.
public sealed class KindEndpointsTests(ApprovalsHost host) : IClassFixture<ApprovalsHost>
{
    [Fact]
    public async Task List_TheDeclaredKinds_ByTitleToEveryEmployeeWithTheirStepsAndTheWorkingDaysTheyDeclare()
    {
        using var employee = host.Employee();

        var kinds = await ApprovalsApi.KindsAsync(employee);

        var expected = TestApprovalKinds.All.OrderBy(kind => kind.Title, StringComparer.Ordinal).ThenBy(kind => kind.Key, StringComparer.Ordinal).Select(kind => kind.Key);
        Assert.Equal(expected, kinds.Select(kind => kind.Kind));
        var untouched = Assert.Single(kinds, kind => kind.Kind == TestApprovalKinds.Untouched.Key);
        Assert.Equal((TestApprovalKinds.Untouched.Title, TestApprovalKinds.Untouched.Description, TestApprovalKinds.WorkingDaysToDecide), (untouched.Title, untouched.Description, untouched.DeclaredWorkingDays));
        Assert.Equal([new ApprovalKindStepResponse(1, TestApprovalKinds.ManagerStepTitle, ApprovalStepRule.Manager, null)], untouched.Steps);
        Assert.Null(untouched.Setting);
        var chain = Assert.Single(kinds, kind => kind.Kind == TestApprovalKinds.Api(1).Key);
        Assert.Equal(
            [
                new ApprovalKindStepResponse(1, TestApprovalKinds.ManagerStepTitle, ApprovalStepRule.Manager, null),
                new ApprovalKindStepResponse(2, TestApprovalKinds.FinanceStepTitle, ApprovalStepRule.Role, ErpRole.Finance),
            ],
            chain.Steps);
    }

    [Fact]
    public async Task SetUp_AKind_AnswersTheKindWithItsSettingAtTheAddressItChangesAtAndReplaysARepeatedKey()
    {
        var kind = TestApprovalKinds.Api(9);
        using var administrator = host.Administrator();
        var key = Guid.CreateVersion7().ToString();

        using var created = await ApprovalsApi.SetUpAsync(administrator, kind, required: false, workingDaysToDecide: 5, key);
        using var replayed = await ApprovalsApi.SetUpAsync(administrator, kind, required: false, workingDaysToDecide: 5, key);
        using var again = await ApprovalsApi.SetUpAsync(administrator, kind, required: true, workingDaysToDecide: 3);

        var answer = await ApprovalsApi.ReadAsync<ApprovalKindResponse>(created, HttpStatusCode.Created);
        var setting = Assert.IsType<ApprovalKindSettingResponse>(answer.Setting);
        Assert.Equal((kind.Key, false, 5), (answer.Kind, setting.Required, setting.WorkingDaysToDecide));
        Assert.Equal(ApprovalsApi.KindSettingsPath(setting.Id).OriginalString, created.Headers.Location?.OriginalString);
        Assert.Equal(setting, (await ApprovalsApi.ReadAsync<ApprovalKindResponse>(replayed, HttpStatusCode.Created)).Setting);
        await ApprovalsApi.AssertProblemAsync(again, HttpStatusCode.Conflict, "approval.setting-exists");
        Assert.Equal(setting, Assert.Single(await ApprovalsApi.KindsAsync(administrator), listed => listed.Kind == kind.Key).Setting);
    }

    [Fact]
    public async Task Change_ASetting_FromItsVersionAndRefusesAnOutdatedOrMalformedVersionOrAnUnknownSetting()
    {
        var kind = TestApprovalKinds.Api(10);
        using var administrator = host.Administrator();
        using var created = await ApprovalsApi.SetUpAsync(administrator, kind, required: true, workingDaysToDecide: 2);
        var setting = (await ApprovalsApi.ReadAsync<ApprovalKindResponse>(created, HttpStatusCode.Created)).Setting!;

        using var changed = await ApprovalsApi.ChangeAsync(administrator, setting.Id, required: false, workingDaysToDecide: 7, setting.Version);
        using var outdated = await ApprovalsApi.ChangeAsync(administrator, setting.Id, required: true, workingDaysToDecide: 3, setting.Version);
        using var malformed = await ApprovalsApi.ChangeAsync(administrator, setting.Id, required: true, workingDaysToDecide: 3, "not-a-version");
        using var unknown = await ApprovalsApi.ChangeAsync(administrator, Guid.CreateVersion7(), required: true, workingDaysToDecide: 3, Convert.ToBase64String(new byte[8]));

        var answer = (await ApprovalsApi.ReadAsync<ApprovalKindResponse>(changed, HttpStatusCode.OK)).Setting!;
        Assert.Equal((setting.Id, false, 7), (answer.Id, answer.Required, answer.WorkingDaysToDecide));
        Assert.NotEqual(setting.Version, answer.Version);
        await ApprovalsApi.AssertProblemAsync(outdated, HttpStatusCode.Conflict, "concurrency.conflict");
        var problem = await ApprovalsApi.AssertProblemAsync(malformed, HttpStatusCode.BadRequest, "approval.version-invalid");
        Assert.Equal(["version"], problem.Errors.Keys);
        await ApprovalsApi.AssertProblemAsync(unknown, HttpStatusCode.NotFound, "approval.setting-not-found");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public async Task SetUp_WorkingDaysOutsideOneToThirty_IsRefusedByTheRequestsValidation(int workingDaysToDecide)
    {
        using var administrator = host.Administrator();

        using var response = await ApprovalsApi.SetUpAsync(administrator, TestApprovalKinds.Api(12), required: true, workingDaysToDecide);

        var problem = await ApprovalsApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, "request.invalid");
        Assert.Equal(["workingDaysToDecide"], problem.Errors.Keys);
    }

    [Fact]
    public async Task SetUp_AKindNoReleaseDeclaresOrWithoutWhetherItNeedsApproval_IsRefusedAndSetsNothingUp()
    {
        var kind = TestApprovalKinds.Api(12);
        using var administrator = host.Administrator();
        using var unknownRequest = new HttpRequestMessage(HttpMethod.Post, ApprovalsApi.KindSettingsPath())
        {
            Content = JsonContent.Create(new { kind = "test.approvals.nowhere-01", required = true, workingDaysToDecide = 2 }),
        };
        unknownRequest.Headers.Add(ApprovalsApi.IdempotencyKeyHeader, Guid.CreateVersion7().ToString());
        using var incompleteRequest = new HttpRequestMessage(HttpMethod.Post, ApprovalsApi.KindSettingsPath())
        {
            Content = JsonContent.Create(new { kind = kind.Key, workingDaysToDecide = 2 }),
        };
        incompleteRequest.Headers.Add(ApprovalsApi.IdempotencyKeyHeader, Guid.CreateVersion7().ToString());

        using var unknown = await administrator.SendAsync(unknownRequest, TestContext.Current.CancellationToken);
        using var incomplete = await administrator.SendAsync(incompleteRequest, TestContext.Current.CancellationToken);

        var unknownProblem = await ApprovalsApi.AssertProblemAsync(unknown, HttpStatusCode.BadRequest, "approval.kind-unknown");
        Assert.Equal(["kind"], unknownProblem.Errors.Keys);
        var incompleteProblem = await ApprovalsApi.AssertProblemAsync(incomplete, HttpStatusCode.BadRequest, "request.invalid");
        Assert.Equal(["required"], incompleteProblem.Errors.Keys);
        Assert.Null(Assert.Single(await ApprovalsApi.KindsAsync(administrator), listed => listed.Kind == kind.Key).Setting);
    }

    [Fact]
    public async Task SetUp_AKindSwitchedOff_LetsItsRecordsThroughWithoutARequestUntilItIsSwitchedOnWithItsNewWorkingDays()
    {
        var kind = TestApprovalKinds.Api(11);
        using var administrator = host.Administrator();
        using var asker = await host.SignInAsync("Rohan Joshi");
        using var created = await ApprovalsApi.SetUpAsync(administrator, kind, required: false, workingDaysToDecide: 2);
        var off = (await ApprovalsApi.ReadAsync<ApprovalKindResponse>(created, HttpStatusCode.Created)).Setting!;

        var through = await ApprovalRequestSeeder.SubmitAsync(host.Factory.Services, new ApprovalRequestSeed(kind.Key, asker.ObjectId), TestContext.Current.CancellationToken);
        using var switchedOn = await ApprovalsApi.ChangeAsync(administrator, off.Id, required: true, workingDaysToDecide: 4, off.Version);
        Assert.Equal(HttpStatusCode.OK, switchedOn.StatusCode);
        var id = await host.SubmitAsync(kind, asker);

        Assert.Equal(new ApprovalSubmitted(null, ApprovalState.Approved), through.Value);
        Assert.Equal([id], (await ApprovalsApi.ListAsync(asker.Client, kind, "mine")).Items.Select(request => request.Id));
        Assert.All((await ApprovalsApi.GetAsync(asker.Client, id)).Steps, step => Assert.Equal(4, step.WorkingDays));
    }
}
