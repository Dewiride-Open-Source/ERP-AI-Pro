using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Dewiride.Erp.ArchitectureTests.Rules;

public sealed class LayerTests
{
    private static readonly Architecture Architecture = ErpAssemblies.Architecture;

    private static readonly IObjectProvider<IType> DomainTypes = Types().That().ResideInNamespaceMatching(@"^Dewiride\.Erp\.Modules\..+\.Domain(\..*)?$").As("domain types");

    private static readonly IObjectProvider<IType> ApplicationTypes = Types().That().ResideInNamespaceMatching(@"^Dewiride\.Erp\.Modules\..+\.Application(\..*)?$").As("application types");

    private static readonly IObjectProvider<IType> EndpointTypes = Types().That().ResideInNamespaceMatching(@"^Dewiride\.Erp\.Modules\..+\.Endpoints(\..*)?$").As("endpoint types");

    private static readonly IObjectProvider<IType> AspNetCoreTypes = Types().That().ResideInNamespaceMatching(@"^Microsoft\.AspNetCore(\..*)?$").As("ASP.NET Core types");

    private static readonly IObjectProvider<IType> EntityFrameworkTypes = Types().That().ResideInNamespaceMatching(@"^Microsoft\.EntityFrameworkCore(\..*)?$").As("EF Core types");

    private static readonly IObjectProvider<IType> DomainDependencies = Types().That()
        .ResideInNamespaceMatching(@"^(System|Dewiride\.Erp\.BuildingBlocks\.Kernel|Dewiride\.Erp\.BuildingBlocks\.SharedKernel|Dewiride\.Erp\.Modules\..+\.Domain)(\..*)?$")
        .As("kernel, shared kernel, System and domain types");

    private static readonly IObjectProvider<IType> PersistenceTypes = Types().That().ResideInNamespaceMatching(@"^Dewiride\.Erp\.Modules\..+\.Persistence(\..*)?$").As("persistence types");

    [Fact]
    public void Domain_DependsOnlyOnKernelSharedKernelAndSystem()
    {
        AssertRule(Types().That().Are(DomainTypes).Should().OnlyDependOn(DomainDependencies).WithoutRequiringPositiveResults());
    }

    [Fact]
    public void Application_DoesNotDependOnAspNetCore()
    {
        AssertRule(Types().That().Are(ApplicationTypes).Should().NotDependOnAny(AspNetCoreTypes).WithoutRequiringPositiveResults());
    }

    [Fact]
    public void Endpoints_DoNotDependOnEntityFrameworkOrPersistence()
    {
        AssertRule(Types().That().Are(EndpointTypes).Should().NotDependOnAny(EntityFrameworkTypes).AndShould().NotDependOnAny(PersistenceTypes).WithoutRequiringPositiveResults());
    }

    [Fact]
    public void Endpoints_DoNotDependOnDomain()
    {
        AssertRule(Types().That().Are(EndpointTypes).Should().NotDependOnAny(DomainTypes).WithoutRequiringPositiveResults());
    }

    [Fact]
    public void Application_DoesNotDependOnEndpoints()
    {
        AssertRule(Types().That().Are(ApplicationTypes).Should().NotDependOnAny(EndpointTypes).WithoutRequiringPositiveResults());
    }

    [Fact]
    public void Handlers_AreSealed()
    {
        AssertRule(Classes().That().ImplementInterface(typeof(BuildingBlocks.Application.Commands.ICommandHandler<,>))
            .Or().ImplementInterface(typeof(BuildingBlocks.Application.Queries.IQueryHandler<,>))
            .Should().BeSealed().WithoutRequiringPositiveResults());
    }

    private static void AssertRule(IArchRule rule)
    {
        var failures = rule.Evaluate(Architecture).Where(r => !r.Passed).Select(r => r.Description).ToList();
        Assert.Empty(failures);
    }
}
