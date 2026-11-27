using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Application;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Commands.DecideApprovalRequest;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Commands.WithdrawApprovalRequest;

namespace Dewiride.Erp.Modules.Platform.Approvals.UnitTests.Requests.Application;

public sealed class RequestCommandHandlerTests
{
    private static readonly IReadOnlySet<ErpRole> Roles = new HashSet<ErpRole> { ErpRole.Employee, ErpRole.Administrator };

    private static readonly byte[] Version = [0, 0, 0, 0, 0, 0, 7, 209];

    private readonly TestActorContext _actor = new(Guid.CreateVersion7(), IsAuthenticated: true);

    [Theory]
    [InlineData(ApprovalDecision.Approve, null)]
    [InlineData(ApprovalDecision.Return, "Attach the hotel bill.")]
    [InlineData(ApprovalDecision.Reject, "The trip was not approved.")]
    public async Task Decide_AVersionTheApiGave_PassesTheDecisionTheCommentAndTheVersionsBytesAsTheActor(ApprovalDecision decision, string? comment)
    {
        var decisions = new RecordingApprovalDecisions();
        var id = ApprovalRequestId.Create();
        var command = new DecideApprovalRequestCommand(id, decision, comment, Convert.ToBase64String(Version), new ApprovalAccess(Roles, SeesAll: true, DecidesAny: true));

        var result = await new DecideApprovalRequestHandler(decisions, _actor).HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.Same(ApprovalSamples.Details, result.Value);
        Assert.Equal(["DecideAsync"], decisions.Calls);
        Assert.Equal((id, decision, comment), (decisions.Id, decisions.Decision, decisions.Comment));
        Assert.Equal(Version, decisions.Version);
        Assert.Equal(new ApprovalCaller(_actor.ActorId, Roles, SeesAll: true, DecidesAny: true), decisions.Caller);
    }

    [Theory]
    [InlineData("not base64")]
    [InlineData("AAAA")]
    public async Task Decide_AVersionTheApiNeverGave_IsRefusedWithoutADecision(string version)
    {
        var decisions = new RecordingApprovalDecisions();
        var command = new DecideApprovalRequestCommand(ApprovalRequestId.Create(), ApprovalDecision.Approve, null, version, new ApprovalAccess(Roles, SeesAll: false, DecidesAny: false));

        var result = await new DecideApprovalRequestHandler(decisions, _actor).HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.Equal(ApprovalErrors.VersionInvalid.Code, result.Error!.Code);
        Assert.Contains(ApprovalErrors.VersionField, result.Error.Fields.Keys);
        Assert.Empty(decisions.Calls);
    }

    [Fact]
    public async Task Withdraw_AVersionTheApiGave_PassesTheReasonAndTheVersionsBytesAsTheActor()
    {
        var decisions = new RecordingApprovalDecisions();
        var id = ApprovalRequestId.Create();
        var command = new WithdrawApprovalRequestCommand(id, "Booked by mistake.", Convert.ToBase64String(Version), new ApprovalAccess(Roles, SeesAll: false, DecidesAny: false));

        var result = await new WithdrawApprovalRequestHandler(decisions, _actor).HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.Same(ApprovalSamples.Details, result.Value);
        Assert.Equal(["WithdrawAsync"], decisions.Calls);
        Assert.Equal((id, "Booked by mistake.", (ApprovalDecision?)null), (decisions.Id, decisions.Comment, decisions.Decision));
        Assert.Equal(Version, decisions.Version);
        Assert.Equal(new ApprovalCaller(_actor.ActorId, Roles, SeesAll: false, DecidesAny: false), decisions.Caller);
    }

    [Theory]
    [InlineData("not base64")]
    [InlineData("AAAA")]
    public async Task Withdraw_AVersionTheApiNeverGave_IsRefusedWithoutAWithdrawal(string version)
    {
        var decisions = new RecordingApprovalDecisions();
        var command = new WithdrawApprovalRequestCommand(ApprovalRequestId.Create(), null, version, new ApprovalAccess(Roles, SeesAll: false, DecidesAny: false));

        var result = await new WithdrawApprovalRequestHandler(decisions, _actor).HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.Equal(ApprovalErrors.VersionInvalid.Code, result.Error!.Code);
        Assert.Contains(ApprovalErrors.VersionField, result.Error.Fields.Keys);
        Assert.Empty(decisions.Calls);
    }
}
