using System.Net;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Shared;
using Dewiride.Erp.Testing.Approvals;

namespace Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Requests.Endpoints;

// The withdrawals read no box, so they share one API kind, each on a record of its own and with people of its own.
public sealed class RequestWithdrawalTests(ApprovalsHost host) : IClassFixture<ApprovalsHost>
{
    private static readonly ApprovalKind Kind = TestApprovalKinds.Api(8);

    [Fact]
    public async Task Withdraw_ByThePersonItIsFor_ClosesTheRequestWithTheReasonOnTheStepThatWaited()
    {
        using var asker = await host.SignInAsync("Asha Rao");
        using var manager = await host.SignInAsync("Ravi Kumar");
        await host.ReportsToAsync(asker, manager);
        var id = await host.SubmitAsync(Kind, asker);

        var withdrawn = await ApprovalsApi.ActAsync(asker.Client, id, "withdraw", "Booked by mistake.");

        Assert.Equal(ApprovalState.Withdrawn, withdrawn.State);
        Assert.NotNull(withdrawn.ClosedAt);
        Assert.Equal((ApprovalStepState.Closed, "Booked by mistake.", (Guid?)null), (withdrawn.Steps[0].State, withdrawn.Steps[0].Comment!, withdrawn.Steps[0].DecidedById));
        Assert.Equal(ApprovalStepState.Closed, withdrawn.Steps[1].State);
        Assert.False(withdrawn.CanWithdraw);
    }

    [Fact]
    public async Task Withdraw_ByTheManagerItWaitsFor_IsRefused()
    {
        using var asker = await host.SignInAsync("Meera Nair");
        using var manager = await host.SignInAsync("Kiran Shah");
        await host.ReportsToAsync(asker, manager);
        var id = await host.SubmitAsync(Kind, asker);
        var request = await ApprovalsApi.GetAsync(manager.Client, id);

        using var response = await ApprovalsApi.PostAsync(manager.Client, id, "withdraw", request.Version);

        await ApprovalsApi.AssertProblemAsync(response, HttpStatusCode.Forbidden, "approval.not-yours-to-withdraw");
        Assert.Equal(ApprovalState.Submitted, (await ApprovalsApi.GetAsync(manager.Client, id)).State);
    }

    [Fact]
    public async Task Withdraw_AClosedRequestOrFromAMalformedVersion_IsRefused()
    {
        using var asker = await host.SignInAsync("Farah Khan");
        var id = await host.SubmitAsync(Kind, asker);
        var withdrawn = await ApprovalsApi.ActAsync(asker.Client, id, "withdraw");

        using var again = await ApprovalsApi.PostAsync(asker.Client, id, "withdraw", withdrawn.Version);
        using var malformed = await ApprovalsApi.PostAsync(asker.Client, id, "withdraw", "not-a-version");

        await ApprovalsApi.AssertProblemAsync(again, HttpStatusCode.Conflict, "approval.not-waiting");
        var problem = await ApprovalsApi.AssertProblemAsync(malformed, HttpStatusCode.BadRequest, "approval.version-invalid");
        Assert.Equal(["version"], problem.Errors.Keys);
    }

    [Fact]
    public async Task Get_ARecordAskedForAgain_ShowsItsEarlierRequestToItsAskerAloneAndWhatEachPersonMayDo()
    {
        using var asker = await host.SignInAsync("Ishaan Bose");
        using var manager = await host.SignInAsync("Sunita Pillai");
        await host.ReportsToAsync(asker, manager);
        var record = Guid.CreateVersion7();
        var earlier = await host.SubmitAsync(Kind, asker, subjectId: record);
        await ApprovalsApi.ActAsync(asker.Client, earlier, "withdraw");
        var id = await host.SubmitAsync(Kind, asker, subjectId: record);

        var asManager = await ApprovalsApi.GetAsync(manager.Client, id);
        var asAsker = await ApprovalsApi.GetAsync(asker.Client, id);

        Assert.Empty(asManager.EarlierRequests);
        var listed = Assert.Single(asAsker.EarlierRequests);
        Assert.Equal((earlier, ApprovalState.Withdrawn), (listed.Id, listed.State));
        Assert.NotNull(listed.ClosedAt);
        Assert.Equal((record, record), (asManager.SubjectId, asAsker.SubjectId));
        Assert.Equal((true, false), (asManager.CanDecide, asManager.CanWithdraw));
        Assert.Equal((false, true), (asAsker.CanDecide, asAsker.CanWithdraw));
    }
}
