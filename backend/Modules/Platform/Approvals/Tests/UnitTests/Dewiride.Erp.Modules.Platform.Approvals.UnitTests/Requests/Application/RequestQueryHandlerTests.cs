using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Application;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Queries.CountApprovalRequests;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Queries.GetApprovalRequest;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Queries.ListApprovalRequests;

namespace Dewiride.Erp.Modules.Platform.Approvals.UnitTests.Requests.Application;

public sealed class RequestQueryHandlerTests
{
    private static readonly IReadOnlySet<ErpRole> Roles = new HashSet<ErpRole> { ErpRole.Employee, ErpRole.Finance };

    private readonly TestActorContext _actor = new(Guid.CreateVersion7(), IsAuthenticated: true);

    [Theory]
    [InlineData(ApprovalBox.Waiting, false)]
    [InlineData(ApprovalBox.Mine, false)]
    [InlineData(ApprovalBox.Decided, false)]
    [InlineData(ApprovalBox.All, true)]
    public async Task List_ABox_AsksTheInboxForItAsTheActorWithTheirAccess(ApprovalBox box, bool seesAll)
    {
        var inbox = new RecordingApprovalInbox();
        var list = ListRequest.Parse(2, 10, "submittedAt:asc", "kind:eq:test.approvals.unit-claims").Value;

        var result = await new ListApprovalRequestsHandler(inbox, _actor).HandleAsync(new ListApprovalRequestsQuery(box, list, new ApprovalAccess(Roles, seesAll, DecidesAny: false)), TestContext.Current.CancellationToken);

        Assert.Same(ApprovalSamples.Page, result.Value);
        Assert.Equal(["ListAsync"], inbox.Calls);
        Assert.Equal((box, list), (inbox.Box, inbox.List));
        Assert.Equal(new ApprovalCaller(_actor.ActorId, Roles, seesAll, DecidesAny: false), inbox.Caller);
    }

    [Fact]
    public async Task Get_ARequest_AsksTheInboxForItsIdAsTheActorWithTheirAccess()
    {
        var inbox = new RecordingApprovalInbox();
        var id = ApprovalRequestId.Create();

        var result = await new GetApprovalRequestHandler(inbox, _actor).HandleAsync(new GetApprovalRequestQuery(id, new ApprovalAccess(Roles, SeesAll: true, DecidesAny: true)), TestContext.Current.CancellationToken);

        Assert.Same(ApprovalSamples.Details, result.Value);
        Assert.Equal(["GetAsync"], inbox.Calls);
        Assert.Equal(id, inbox.Id);
        Assert.Equal(new ApprovalCaller(_actor.ActorId, Roles, SeesAll: true, DecidesAny: true), inbox.Caller);
    }

    [Fact]
    public async Task Count_Always_AnswersTheInboxsCountsForTheActor()
    {
        var inbox = new RecordingApprovalInbox();

        var result = await new CountApprovalRequestsHandler(inbox, _actor).HandleAsync(new CountApprovalRequestsQuery(new ApprovalAccess(Roles, SeesAll: false, DecidesAny: false)), TestContext.Current.CancellationToken);

        Assert.Equal(ApprovalSamples.Counts, result.Value);
        Assert.Equal(["CountAsync"], inbox.Calls);
        Assert.Equal(new ApprovalCaller(_actor.ActorId, Roles, SeesAll: false, DecidesAny: false), inbox.Caller);
    }
}
