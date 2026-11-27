using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Outcomes;
using Dewiride.Erp.BuildingBlocks.Approvals.Persistence;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Approvals;

public sealed class ApprovalKindCatalogTests
{
    private static readonly ApprovalKind Claims = new(
        "test.approvals.claims",
        "Expense claim",
        "A claim waits for approval before it is paid.",
        "test.approvals.claims",
        [ApprovalStepDefinition.Manager("Manager"), ApprovalStepDefinition.HeldBy("Finance", ErpRole.Finance)],
        2);

    private static readonly ApprovalKind Bills = new(
        "test.approvals.bills",
        "Bill approval",
        "A bill waits for approval before it is posted.",
        "test.approvals.bills",
        [ApprovalStepDefinition.HeldBy("Finance", ErpRole.Finance)],
        3);

    [Fact]
    public void Catalog_OfDeclaredKinds_ListsThemByTitleAndFindsEachByKey()
    {
        var catalog = new ApprovalKindCatalog([Registration(Claims), Registration(Bills)]);

        Assert.Equal([Bills, Claims], catalog.Kinds.Select(registration => registration.Kind));
        Assert.Same(Claims, catalog.Find("test.approvals.claims")?.Kind);
        Assert.Null(catalog.Find("test.approvals.payouts"));
        Assert.Equal("Expense claim", catalog.TitleOf("test.approvals.claims"));
        Assert.Equal("test.approvals.payouts", catalog.TitleOf("test.approvals.payouts"));
    }

    [Fact]
    public void Catalog_AKindDeclaredTwice_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ApprovalKindCatalog([Registration(Claims), Registration(Claims with { Title = "Other" })]));
    }

    [Fact]
    public void AddApprovalKind_AKind_JoinsTheCatalogWithItsContextAndHandler()
    {
        using var provider = new ServiceCollection().AddApprovalKind<ApprovalsDbContext, RecordingOutcomes>(Claims).AddApprovalKind<ApprovalsDbContext, RecordingOutcomes>(Bills).BuildServiceProvider();

        var catalog = new ApprovalKindCatalog(provider.GetServices<ApprovalKindRegistration>());

        Assert.Equal(2, catalog.Kinds.Count);
        Assert.Equal((typeof(ApprovalsDbContext), typeof(RecordingOutcomes)), (catalog.Find(Claims.Key)!.ContextType, catalog.Find(Claims.Key)!.HandlerType));
    }

    [Fact]
    public void AddApprovalKind_TheSameKeyTwice_Throws()
    {
        var services = new ServiceCollection().AddApprovalKind<ApprovalsDbContext, RecordingOutcomes>(Claims);

        Assert.Throws<InvalidOperationException>(() => services.AddApprovalKind<ApprovalsDbContext, RecordingOutcomes>(Claims with { Title = "Other" }));
    }

    [Fact]
    public void AddApprovalKind_AnAbstractHandler_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddApprovalKind<ApprovalsDbContext, AbstractOutcomes>(Claims));
    }

    [Theory]
    [InlineData("Test.approvals.claims")]
    [InlineData("test.approvals")]
    [InlineData("test..claims")]
    [InlineData("test.approvals.claims\n")]
    public void AddApprovalKind_AKeyThatIsNotThreeKebabSegments_Throws(string key)
    {
        Assert.Throws<ArgumentException>(() => Declare(Claims with { Key = key }));
    }

    [Theory]
    [InlineData("", "A claim waits for approval before it is paid.")]
    [InlineData("Expense claim", " ")]
    public void AddApprovalKind_ABlankTitleOrDescription_Throws(string title, string description)
    {
        Assert.Throws<ArgumentException>(() => Declare(Claims with { Title = title, Description = description }));
    }

    [Fact]
    public void AddApprovalKind_ATitleOrDescriptionTooLong_Throws()
    {
        Assert.Throws<ArgumentException>(() => Declare(Claims with { Title = new string('a', ApprovalKind.TitleMaxLength + 1) }));
        Assert.Throws<ArgumentException>(() => Declare(Claims with { Description = new string('a', ApprovalKind.DescriptionMaxLength + 1) }));
    }

    [Theory]
    [InlineData("Test.approvals.claims")]
    [InlineData("test.claims")]
    [InlineData("test.approvals.claims-of-a-very-long-name-past-the-sixty-four-characters")]
    public void AddApprovalKind_ASubjectTypeThatIsNotAChangeKey_Throws(string subjectType)
    {
        Assert.Throws<ArgumentException>(() => Declare(Claims with { SubjectType = subjectType }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void AddApprovalKind_WorkingDaysOutsideOneToThirty_Throws(int workingDays)
    {
        Assert.Throws<ArgumentException>(() => Declare(Claims with { WorkingDaysToDecide = workingDays }));
    }

    [Fact]
    public void AddApprovalKind_NoStepsOrMoreThanFive_Throws()
    {
        Assert.Throws<ArgumentException>(() => Declare(Claims with { Steps = [] }));
        Assert.Throws<ArgumentException>(() => Declare(Claims with { Steps = [.. Enumerable.Range(1, ApprovalKind.MaxSteps + 1).Select(step => ApprovalStepDefinition.Manager($"Manager {step}"))] }));
    }

    [Fact]
    public void AddApprovalKind_TwoStepsOfOneTitle_Throws()
    {
        Assert.Throws<ArgumentException>(() => Declare(Claims with { Steps = [ApprovalStepDefinition.Manager("Manager"), ApprovalStepDefinition.HeldBy("manager", ErpRole.Manager)] }));
    }

    [Fact]
    public void AddApprovalKind_AStepTitleBlankOrTooLong_Throws()
    {
        Assert.Throws<ArgumentException>(() => Declare(Claims with { Steps = [ApprovalStepDefinition.Manager(" ")] }));
        Assert.Throws<ArgumentException>(() => Declare(Claims with { Steps = [ApprovalStepDefinition.Manager(new string('a', ApprovalStepDefinition.TitleMaxLength + 1))] }));
    }

    [Theory]
    [InlineData(ErpRole.Employee)]
    [InlineData((ErpRole)42)]
    public void AddApprovalKind_ARoleStepNamingEmployeeOrNoRole_Throws(ErpRole role)
    {
        Assert.Throws<ArgumentException>(() => Declare(Claims with { Steps = [ApprovalStepDefinition.HeldBy("Approver", role)] }));
    }

    [Theory]
    [InlineData(ErpRole.Finance)]
    [InlineData(ErpRole.Hr)]
    [InlineData(ErpRole.Manager)]
    [InlineData(ErpRole.Administrator)]
    public void AddApprovalKind_ARoleStepNamingAnAssignedRole_IsDeclared(ErpRole role)
    {
        var services = Declare(Claims with { Steps = [ApprovalStepDefinition.HeldBy("Approver", role)] });

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ApprovalKindRegistration));
    }

    private static ServiceCollection Declare(ApprovalKind kind)
    {
        var services = new ServiceCollection();
        services.AddApprovalKind<ApprovalsDbContext, RecordingOutcomes>(kind);

        return services;
    }

    private static ApprovalKindRegistration Registration(ApprovalKind kind) => new(kind, typeof(ApprovalsDbContext), typeof(RecordingOutcomes));

    private sealed class RecordingOutcomes : IApprovalOutcomeHandler
    {
        public Task<Result> HandleAsync(ApprovalOutcome outcome, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    }

    private abstract class AbstractOutcomes : IApprovalOutcomeHandler
    {
        public abstract Task<Result> HandleAsync(ApprovalOutcome outcome, CancellationToken cancellationToken);
    }
}
