using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Approvals;

public sealed class ApprovalRoutingTests
{
    private static readonly DateOnly Today = new(2026, 11, 27);

    private static readonly Guid Asker = new("01999a01-0000-7000-8000-000000000001");

    private static readonly Guid Submitter = new("01999a01-0000-7000-8000-000000000002");

    private static readonly Guid Left = new("01999a01-0000-7000-8000-000000000003");

    private static readonly Guid Nearest = new("01999a01-0000-7000-8000-000000000004");

    private static readonly Guid Above = new("01999a01-0000-7000-8000-000000000005");

    private static readonly ApprovalKind ManagerThenFinance = Kind(ApprovalStepDefinition.Manager("Manager"), ApprovalStepDefinition.HeldBy("Finance", ErpRole.Finance));

    [Fact]
    public async Task PlanAsync_AManagerStep_GoesToTheNearestManagerWhoIsActiveAndHasAnAccount()
    {
        var managers = new ScriptedReportingManagers();
        managers.Set(Asker, new ReportingManager(Left, IsActive: false), new ReportingManager(null, IsActive: true), new ReportingManager(Nearest, IsActive: true), new ReportingManager(Above, IsActive: true));

        var plans = await PlanAsync(managers, Kind(ApprovalStepDefinition.Manager("Manager")), Asker);

        var plan = Assert.Single(plans);
        Assert.Equal((ApproverKind.Person, (Guid?)Nearest, (ErpRole?)null), (plan.ApproverKind, plan.ApproverId, plan.ApproverRole));
        Assert.Equal(("Manager", ApprovalStepRule.Manager, 3), (plan.Title, plan.Rule, plan.WorkingDays));
        Assert.Equal([(Asker, Today)], managers.Asked);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PlanAsync_AManagerWhoSubmittedOrAskedForTheRequest_IsPassedOver(bool submitted)
    {
        var managers = new ScriptedReportingManagers();
        var passedOver = submitted ? Submitter : Asker;
        managers.Set(Asker, new ReportingManager(passedOver, IsActive: true), new ReportingManager(Nearest, IsActive: true));

        var plans = await PlanAsync(managers, Kind(ApprovalStepDefinition.Manager("Manager")), Asker, Submitter);

        Assert.Equal(Nearest, Assert.Single(plans).ApproverId);
    }

    [Fact]
    public async Task PlanAsync_NobodyLeftToDecideAManagerStep_GoesToTheAdministrators()
    {
        var managers = new ScriptedReportingManagers();
        managers.Set(Asker, new ReportingManager(Left, IsActive: false), new ReportingManager(Submitter, IsActive: true));
        var nobody = Guid.CreateVersion7();

        var withNobodyLeft = await PlanAsync(managers, Kind(ApprovalStepDefinition.Manager("Manager")), Asker, Submitter);
        var withoutAManager = await PlanAsync(managers, Kind(ApprovalStepDefinition.Manager("Manager")), nobody);

        Assert.All(withNobodyLeft.Concat(withoutAManager), plan => Assert.Equal((ApproverKind.Role, (Guid?)null, (ErpRole?)ErpRole.Administrator), (plan.ApproverKind, plan.ApproverId, plan.ApproverRole)));
        Assert.All(withNobodyLeft.Concat(withoutAManager), plan => Assert.Equal(ApprovalStepRule.Manager, plan.Rule));
    }

    [Fact]
    public async Task PlanAsync_ManagerThenFinance_SettlesBothStepsAtOnce()
    {
        var managers = new ScriptedReportingManagers();
        managers.Set(Asker, new ReportingManager(Nearest, IsActive: true));

        var plans = await PlanAsync(managers, ManagerThenFinance, Asker);

        Assert.Equal(
            [
                ("Manager", ApprovalStepRule.Manager, ApproverKind.Person, (Guid?)Nearest, (ErpRole?)null),
                ("Finance", ApprovalStepRule.Role, ApproverKind.Role, null, ErpRole.Finance),
            ],
            plans.Select(plan => (plan.Title, plan.Rule, plan.ApproverKind, plan.ApproverId, plan.ApproverRole)));
    }

    [Fact]
    public async Task PlanAsync_TwoManagerSteps_AsksForTheReportingLinesOnce()
    {
        var managers = new ScriptedReportingManagers();
        managers.Set(Asker, new ReportingManager(Nearest, IsActive: true));

        var plans = await PlanAsync(managers, Kind(ApprovalStepDefinition.Manager("Manager"), ApprovalStepDefinition.Manager("Manager again")), Asker);

        Assert.Equal([Nearest, Nearest], plans.Select(plan => plan.ApproverId!.Value));
        Assert.Single(managers.Asked);
    }

    [Fact]
    public async Task PlanAsync_AKindDecidedByRolesAlone_NeedsNoReportingLines()
    {
        var plans = await PlanAsync(managers: null, Kind(ApprovalStepDefinition.HeldBy("Finance", ErpRole.Finance), ApprovalStepDefinition.HeldBy("HR", ErpRole.Hr)), Asker);

        Assert.Equal([ErpRole.Finance, ErpRole.Hr], plans.Select(plan => plan.ApproverRole!.Value));
    }

    [Fact]
    public async Task PlanAsync_AManagerStepWithoutReportingLines_Throws()
    {
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => PlanAsync(managers: null, ManagerThenFinance, Asker));

        Assert.Contains(nameof(IReportingManagers), thrown.Message, StringComparison.Ordinal);
    }

    private static ApprovalKind Kind(params ApprovalStepDefinition[] steps) =>
        new("test.approvals.claims", "Expense claim", "A claim waits for approval before it is paid.", "test.approvals.claims", steps, 3);

    private static async Task<IReadOnlyList<ApprovalStepPlan>> PlanAsync(ScriptedReportingManagers? managers, ApprovalKind kind, Guid requestedFor, Guid? submittedBy = null)
    {
        var services = new ServiceCollection();
        if (managers is not null)
        {
            services.AddSingleton<IReportingManagers>(managers);
        }

        await using var provider = services.BuildServiceProvider();

        return await new ApprovalRouting(provider).PlanAsync(kind, kind.WorkingDaysToDecide, requestedFor, submittedBy ?? requestedFor, Today, TestContext.Current.CancellationToken);
    }
}
