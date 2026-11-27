using System.Net;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Shared;
using Dewiride.Erp.Testing.Approvals;

namespace Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Requests.Endpoints;

// The decisions read no box, so they share one API kind, each on a record of its own and with people of its own.
public sealed class RequestDecisionTests(ApprovalsHost host) : IClassFixture<ApprovalsHost>
{
    private static readonly ApprovalKind Kind = TestApprovalKinds.Api(7);

    [Fact]
    public async Task Approve_EachStepByItsApprover_ApprovesTheRequestWithEveryDecisionRecorded()
    {
        using var asker = await host.SignInAsync("Asha Rao");
        using var manager = await host.SignInAsync("Ravi Kumar");
        using var finance = await host.SignInAsync("Lakshmi Iyer");
        await host.ReportsToAsync(asker, manager);
        await host.AssignAsync(finance, "finance");
        var id = await host.SubmitAsync(Kind, asker);

        var asManager = await ApprovalsApi.GetAsync(manager.Client, id);
        var afterManager = await ApprovalsApi.ActAsync(manager.Client, id, "approve", "Looks right.");
        var asFinance = await ApprovalsApi.GetAsync(finance.Client, id);
        var approved = await ApprovalsApi.ActAsync(finance.Client, id, "approve");
        var asAsker = await ApprovalsApi.GetAsync(asker.Client, id);

        Assert.Equal((true, false, false), (asManager.CanDecide, asManager.DecidesInPlace, asManager.CanWithdraw));
        Assert.All(asManager.Steps, step => Assert.Equal(TestApprovalKinds.WorkingDaysToDecide, step.WorkingDays));
        Assert.Equal((ApprovalState.Submitted, TestApprovalKinds.FinanceStepTitle, (ErpRole?)ErpRole.Finance), (afterManager.State, afterManager.WaitingStepTitle!, afterManager.WaitingForRole));
        var managerStep = afterManager.Steps[0];
        Assert.Equal((ApprovalStepState.Approved, (Guid?)manager.ObjectId, manager.Name, false, "Looks right."), (managerStep.State, managerStep.DecidedById, managerStep.DecidedByName!, managerStep.InPlaceOfApprover, managerStep.Comment!));
        Assert.Equal((ApprovalStepState.Waiting, ApproverKind.Role, (ErpRole?)ErpRole.Finance), (afterManager.Steps[1].State, afterManager.Steps[1].ApproverKind, afterManager.Steps[1].ApproverRole));
        Assert.True(asFinance.CanDecide);
        Assert.Equal(ApprovalState.Approved, approved.State);
        Assert.NotNull(approved.ClosedAt);
        Assert.Null(approved.WaitingStepTitle);
        Assert.Equal((ApprovalStepState.Approved, (Guid?)finance.ObjectId), (approved.Steps[1].State, approved.Steps[1].DecidedById));
        Assert.Equal((false, false), (asAsker.CanDecide, asAsker.CanWithdraw));
    }

    [Fact]
    public async Task Return_WithoutAReason_IsRefusedKeyedByTheCommentAndWithOneSendsTheRequestBack()
    {
        using var asker = await host.SignInAsync("Meera Nair");
        using var manager = await host.SignInAsync("Kiran Shah");
        await host.ReportsToAsync(asker, manager);
        var id = await host.SubmitAsync(Kind, asker);
        var request = await ApprovalsApi.GetAsync(manager.Client, id);

        using var withoutReason = await ApprovalsApi.PostAsync(manager.Client, id, "return", request.Version, "   ");
        var returned = await ApprovalsApi.ActAsync(manager.Client, id, "return", "Attach the hotel bill.");

        var problem = await ApprovalsApi.AssertProblemAsync(withoutReason, HttpStatusCode.BadRequest, "approval.reason-required");
        Assert.Equal(["comment"], problem.Errors.Keys);
        Assert.Equal(ApprovalState.Returned, returned.State);
        Assert.Equal((ApprovalStepState.Returned, "Attach the hotel bill."), (returned.Steps[0].State, returned.Steps[0].Comment!));
        Assert.Equal(ApprovalStepState.Closed, returned.Steps[1].State);
    }

    [Fact]
    public async Task Reject_WithoutAReason_IsRefusedAndWithOneRejectsTheRequestAndClosesItsLaterStep()
    {
        using var asker = await host.SignInAsync("Farah Khan");
        using var manager = await host.SignInAsync("Deepak Verma");
        await host.ReportsToAsync(asker, manager);
        var id = await host.SubmitAsync(Kind, asker);
        var request = await ApprovalsApi.GetAsync(manager.Client, id);

        using var withoutReason = await ApprovalsApi.PostAsync(manager.Client, id, "reject", request.Version);
        var rejected = await ApprovalsApi.ActAsync(manager.Client, id, "reject", "The trip was not approved.");

        var problem = await ApprovalsApi.AssertProblemAsync(withoutReason, HttpStatusCode.BadRequest, "approval.reason-required");
        Assert.Equal(["comment"], problem.Errors.Keys);
        Assert.Equal(ApprovalState.Rejected, rejected.State);
        Assert.Equal((ApprovalStepState.Rejected, "The trip was not approved."), (rejected.Steps[0].State, rejected.Steps[0].Comment!));
        Assert.Equal(ApprovalStepState.Closed, rejected.Steps[1].State);
    }

    [Fact]
    public async Task Approve_InPlaceOfTheManager_AnAdministratorDecidesAndTheStepSaysSo()
    {
        using var asker = await host.SignInAsync("Ishaan Bose");
        using var manager = await host.SignInAsync("Sunita Pillai");
        using var administrator = await host.SignInAsync("Kavya Menon", administrator: true);
        await host.ReportsToAsync(asker, manager);
        var id = await host.SubmitAsync(Kind, asker);

        var before = await ApprovalsApi.GetAsync(administrator.Client, id);
        var decided = await ApprovalsApi.ActAsync(administrator.Client, id, "approve");

        Assert.Equal((true, true), (before.CanDecide, before.DecidesInPlace));
        var step = decided.Steps[0];
        Assert.Equal((ApprovalStepState.Approved, (Guid?)administrator.ObjectId, administrator.Name, true), (step.State, step.DecidedById, step.DecidedByName!, step.InPlaceOfApprover));
        Assert.Equal(((Guid?)manager.ObjectId, manager.Name), (step.ApproverId, step.ApproverName!));
    }

    [Fact]
    public async Task Approve_ARequestTheCallerAskedFor_IsRefusedEvenToAnAdministrator()
    {
        using var asker = await host.SignInAsync("Vikram Iyer", administrator: true);
        var id = await host.SubmitAsync(Kind, asker);

        var request = await ApprovalsApi.GetAsync(asker.Client, id);
        using var response = await ApprovalsApi.PostAsync(asker.Client, id, "approve", request.Version);

        Assert.Equal((false, true), (request.CanDecide, request.CanWithdraw));
        Assert.Equal(ErpRole.Administrator, request.WaitingForRole);
        await ApprovalsApi.AssertProblemAsync(response, HttpStatusCode.Forbidden, "approval.own-request");
    }

    [Fact]
    public async Task Approve_AStepThatNoLongerWaitsForTheCaller_IsRefused()
    {
        using var asker = await host.SignInAsync("Neha Gupta");
        using var manager = await host.SignInAsync("Rahul Mehta");
        await host.ReportsToAsync(asker, manager);
        var id = await host.SubmitAsync(Kind, asker);
        var afterManager = await ApprovalsApi.ActAsync(manager.Client, id, "approve");

        using var again = await ApprovalsApi.PostAsync(manager.Client, id, "approve", afterManager.Version);

        await ApprovalsApi.AssertProblemAsync(again, HttpStatusCode.Forbidden, "approval.not-yours");
    }

    [Fact]
    public async Task Approve_FromAnOutdatedOrMalformedVersionOrOnceClosed_IsRefused()
    {
        using var asker = await host.SignInAsync("Anil Rao");
        using var manager = await host.SignInAsync("Pooja Shetty");
        using var finance = await host.SignInAsync("Gopal Krishnan");
        await host.ReportsToAsync(asker, manager);
        await host.AssignAsync(finance, "finance");
        var id = await host.SubmitAsync(Kind, asker);
        var submitted = await ApprovalsApi.GetAsync(manager.Client, id);
        await ApprovalsApi.ActAsync(manager.Client, id, "approve");

        using var outdated = await ApprovalsApi.PostAsync(finance.Client, id, "approve", submitted.Version);
        using var malformed = await ApprovalsApi.PostAsync(finance.Client, id, "approve", "not-a-version");
        var approved = await ApprovalsApi.ActAsync(finance.Client, id, "approve");
        using var closed = await ApprovalsApi.PostAsync(finance.Client, id, "approve", approved.Version);

        await ApprovalsApi.AssertProblemAsync(outdated, HttpStatusCode.Conflict, "concurrency.conflict");
        var problem = await ApprovalsApi.AssertProblemAsync(malformed, HttpStatusCode.BadRequest, "approval.version-invalid");
        Assert.Equal(["version"], problem.Errors.Keys);
        await ApprovalsApi.AssertProblemAsync(closed, HttpStatusCode.Conflict, "approval.not-waiting");
    }

    [Fact]
    public async Task GetAndApprove_ARequestTheCallerMayNotSeeOrNoneAtAll_AnswerAsAnUnknownRequest()
    {
        using var asker = await host.SignInAsync("Tara Menon");
        using var stranger = await host.SignInAsync("Aditya Nair");
        var id = await host.SubmitAsync(Kind, asker);

        using var hidden = await stranger.Client.GetAsync(ApprovalsApi.RequestPath(id), TestContext.Current.CancellationToken);
        using var unknown = await stranger.Client.GetAsync(ApprovalsApi.RequestPath(Guid.CreateVersion7()), TestContext.Current.CancellationToken);
        using var approve = await ApprovalsApi.PostAsync(stranger.Client, id, "approve", Convert.ToBase64String(new byte[8]));

        await ApprovalsApi.AssertProblemAsync(hidden, HttpStatusCode.NotFound, "approval.request-not-found");
        await ApprovalsApi.AssertProblemAsync(unknown, HttpStatusCode.NotFound, "approval.request-not-found");
        await ApprovalsApi.AssertProblemAsync(approve, HttpStatusCode.NotFound, "approval.request-not-found");
    }

    [Fact]
    public async Task Approve_ARecordItsModuleRefusesToApprove_LeavesTheRequestWaitingWhileItCanStillBeSentBack()
    {
        var kind = TestApprovalKinds.Refused(1);
        using var asker = await host.SignInAsync("Rohan Joshi");
        using var manager = await host.SignInAsync("Divya Rao");
        await host.ReportsToAsync(asker, manager);
        var id = await host.SubmitAsync(kind, asker);
        var submitted = await ApprovalsApi.GetAsync(manager.Client, id);

        using var refused = await ApprovalsApi.PostAsync(manager.Client, id, "approve", submitted.Version);
        var stillWaiting = await ApprovalsApi.GetAsync(manager.Client, id);
        var returned = await ApprovalsApi.ActAsync(manager.Client, id, "return", "Correct the amount.");

        var problem = await ApprovalsApi.AssertProblemAsync(refused, HttpStatusCode.UnprocessableEntity, RefusingApprovalOutcomes.RefusedCode);
        Assert.Equal(RefusingApprovalOutcomes.RefusedMessage, problem.Detail);
        Assert.Equal((ApprovalState.Submitted, submitted.Version, ApprovalStepState.Waiting), (stillWaiting.State, stillWaiting.Version, stillWaiting.Steps[0].State));
        Assert.Equal(ApprovalState.Returned, returned.State);
    }

    [Fact]
    public async Task Approve_WithoutAVersionOrWithACommentTooLong_IsRefusedByTheRequestsValidation()
    {
        using var asker = await host.SignInAsync("Sameer Kulkarni");
        var id = await host.SubmitAsync(Kind, asker);
        using var administrator = host.Administrator();
        var request = await ApprovalsApi.GetAsync(administrator, id);

        using var withoutVersion = await administrator.PostAsJsonAsync(ApprovalsApi.ActionPath(id, "approve"), new { comment = "Fine." }, TestContext.Current.CancellationToken);
        using var tooLong = await ApprovalsApi.PostAsync(administrator, id, "approve", request.Version, new string('a', ApprovalStep.CommentMaxLength + 1));

        var versionProblem = await ApprovalsApi.AssertProblemAsync(withoutVersion, HttpStatusCode.BadRequest, "request.invalid");
        Assert.Equal(["version"], versionProblem.Errors.Keys);
        var commentProblem = await ApprovalsApi.AssertProblemAsync(tooLong, HttpStatusCode.BadRequest, "request.invalid");
        Assert.Equal(["comment"], commentProblem.Errors.Keys);
        Assert.Equal(ApprovalState.Submitted, (await ApprovalsApi.GetAsync(administrator, id)).State);
    }
}
