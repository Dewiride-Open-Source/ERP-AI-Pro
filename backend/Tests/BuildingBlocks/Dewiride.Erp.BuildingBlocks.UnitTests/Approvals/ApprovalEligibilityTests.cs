using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Approvals;

public sealed class ApprovalEligibilityTests
{
    private static readonly DateTimeOffset SubmittedAt = new(2026, 11, 23, 4, 30, 0, TimeSpan.Zero);

    private static readonly Guid Asker = new("01999a02-0000-7000-8000-000000000001");

    private static readonly Guid Submitter = new("01999a02-0000-7000-8000-000000000002");

    private static readonly Guid Manager = new("01999a02-0000-7000-8000-000000000003");

    private static readonly Guid Stranger = new("01999a02-0000-7000-8000-000000000004");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EligibilityOf_ThePersonItIsForOrWhoSubmittedIt_IsTheirOwnRequestWhateverTheyMayDecide(bool asker)
    {
        var request = ManagerThenFinance();
        var caller = Caller(asker ? Asker : Submitter, seesAll: true, decidesAny: true, ErpRole.Administrator, ErpRole.Finance);

        Assert.Equal(ApprovalEligibility.OwnRequest, ApprovalRouting.EligibilityOf(request, caller));
    }

    [Fact]
    public void EligibilityOf_ThePersonTheWaitingStepWaitsFor_IsItsApprover()
    {
        Assert.Equal(ApprovalEligibility.Approver, ApprovalRouting.EligibilityOf(ManagerThenFinance(), Caller(Manager)));
    }

    [Fact]
    public void EligibilityOf_AHolderOfTheWaitingStepsRole_IsItsApprover()
    {
        var request = ManagerThenFinance();
        Assert.True(request.Approve(Manager, inPlaceOfApprover: false, comment: null, SubmittedAt.AddHours(1)).IsSuccess);

        Assert.Equal(ApprovalEligibility.Approver, ApprovalRouting.EligibilityOf(request, Caller(Stranger, seesAll: false, decidesAny: false, ErpRole.Finance)));
        Assert.Equal(ApprovalEligibility.None, ApprovalRouting.EligibilityOf(request, Caller(Stranger, seesAll: false, decidesAny: false, ErpRole.Hr)));
    }

    [Fact]
    public void EligibilityOf_APersonWhoMayDecideAnyRequest_DecidesInPlaceOfTheApprover()
    {
        Assert.Equal(ApprovalEligibility.InPlaceOfApprover, ApprovalRouting.EligibilityOf(ManagerThenFinance(), Caller(Stranger, seesAll: true, decidesAny: true, ErpRole.Administrator)));
    }

    [Fact]
    public void EligibilityOf_APersonTheRequestDoesNotWaitFor_IsNone()
    {
        Assert.Equal(ApprovalEligibility.None, ApprovalRouting.EligibilityOf(ManagerThenFinance(), Caller(Stranger, seesAll: true, decidesAny: false, ErpRole.Finance)));
    }

    [Fact]
    public void EligibilityOf_AClosedRequest_IsNoneEvenForItsApprover()
    {
        var request = ManagerThenFinance();
        Assert.True(request.Reject(Manager, inPlaceOfApprover: false, "No.", SubmittedAt.AddHours(1)).IsSuccess);

        Assert.Equal(ApprovalEligibility.None, ApprovalRouting.EligibilityOf(request, Caller(Manager, seesAll: true, decidesAny: true, ErpRole.Finance)));
    }

    [Fact]
    public void IsVisibleTo_TheAskerTheSubmitterTheApproverAndAPersonWhoSeesAll_IsTrue()
    {
        var request = ManagerThenFinance();

        Assert.True(ApprovalVisibility.IsVisibleTo(request, Caller(Asker)));
        Assert.True(ApprovalVisibility.IsVisibleTo(request, Caller(Submitter)));
        Assert.True(ApprovalVisibility.IsVisibleTo(request, Caller(Manager)));
        Assert.True(ApprovalVisibility.IsVisibleTo(request, Caller(Stranger, seesAll: true, decidesAny: false)));
    }

    [Fact]
    public void IsVisibleTo_AHolderOfALaterStepsRoleOrADeciderOfAnyRequestAlone_IsFalseUntilTheStepWaitsForThem()
    {
        var request = ManagerThenFinance();
        var financeHolder = Caller(Stranger, seesAll: false, decidesAny: false, ErpRole.Finance);

        Assert.False(ApprovalVisibility.IsVisibleTo(request, financeHolder));
        Assert.False(ApprovalVisibility.IsVisibleTo(request, Caller(Stranger, seesAll: false, decidesAny: true, ErpRole.Administrator)));
        Assert.True(request.Approve(Manager, inPlaceOfApprover: false, comment: null, SubmittedAt.AddHours(1)).IsSuccess);
        Assert.True(ApprovalVisibility.IsVisibleTo(request, financeHolder));
    }

    [Fact]
    public void IsVisibleTo_APersonWhoDecidedAStep_StaysTrueOnceTheRequestIsClosed()
    {
        var request = ManagerThenFinance();
        Assert.True(request.Return(Manager, inPlaceOfApprover: false, "Add the receipt.", SubmittedAt.AddHours(1)).IsSuccess);

        Assert.True(ApprovalVisibility.IsVisibleTo(request, Caller(Manager)));
        Assert.False(ApprovalVisibility.IsVisibleTo(request, Caller(Stranger, seesAll: false, decidesAny: false, ErpRole.Finance)));
    }

    private static ApprovalRequest ManagerThenFinance() =>
        SubmittedRequests.Of(Asker, Submitter, SubmittedAt, SubmittedRequests.ManagerStep(Manager), SubmittedRequests.RoleStep("Finance", ErpRole.Finance));

    private static ApprovalCaller Caller(Guid person, bool seesAll = false, bool decidesAny = false, params ErpRole[] roles) =>
        new(person, new HashSet<ErpRole>([ErpRole.Employee, .. roles]), seesAll, decidesAny);
}
