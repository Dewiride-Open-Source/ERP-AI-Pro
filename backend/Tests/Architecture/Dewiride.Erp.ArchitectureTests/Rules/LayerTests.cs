using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;
using ArchUnitNET.Fluent;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Dewiride.Erp.ArchitectureTests.Rules;

public sealed class LayerTests
{
    private const string AzureStorageNamespace = "Azure.Storage";

    private const string BuildingBlocksNamespace = "Dewiride.Erp.BuildingBlocks";

    private const string StateSetter = "set_" + nameof(EntityEntry.State);

    private const string AuthenticationAssembly = "Dewiride.Erp.BuildingBlocks.Authentication";

    private const string CachingAssembly = "Dewiride.Erp.BuildingBlocks.Caching";

    private const string IdentityWebAssemblyPrefix = "Microsoft.Identity.Web";

    private const string TestingAssemblyPrefix = "Dewiride.Erp.Testing";

    private static readonly Architecture Architecture = ErpAssemblies.Architecture;

    private static readonly IObjectProvider<IType> DomainTypes = Types().That().ResideInNamespaceMatching(@"^Dewiride\.Erp\.Modules\..+\.Domain(\..*)?$").As("domain types");

    private static readonly IObjectProvider<IType> ApplicationTypes = Types().That().ResideInNamespaceMatching(@"^Dewiride\.Erp\.Modules\..+\.Application(\..*)?$").As("application types");

    private static readonly IObjectProvider<IType> EndpointTypes = Types().That().ResideInNamespaceMatching(@"^Dewiride\.Erp\.Modules\..+\.Endpoints(\..*)?$").As("endpoint types");

    private static readonly IObjectProvider<IType> AspNetCoreTypes = Types(true).That().ResideInNamespaceOrBelow("Microsoft.AspNetCore").As("ASP.NET Core types");

    private static readonly IObjectProvider<IType> EntityFrameworkTypes = Types(true).That().ResideInNamespaceOrBelow("Microsoft.EntityFrameworkCore").As("EF Core types");

    private static readonly IObjectProvider<IType> DomainDependencies = Types(true).That()
        .ResideInNamespaceMatching(@"^(System|Dewiride\.Erp\.BuildingBlocks\.Kernel|Dewiride\.Erp\.BuildingBlocks\.SharedKernel|Dewiride\.Erp\.Modules\..+\.Domain)(\..*)?$")
        .As("kernel, shared kernel, System and domain types");

    private static readonly IObjectProvider<IType> PersistenceTypes = Types().That().ResideInNamespaceMatching(@"^Dewiride\.Erp\.Modules\..+\.Persistence(\..*)?$").As("persistence types");

    private static readonly IObjectProvider<IType> HostingTypes = Types().That().ResideInNamespaceMatching(@"^Dewiride\.Erp\.Modules\..+\.Hosting(\..*)?$").As("hosting types");

    private static readonly IObjectProvider<IType> BlobStorageTypes = Types().That().ResideInNamespaceOrBelow("Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob").As("blob storage types");

    private static readonly IObjectProvider<IType> AzureStorageTypes = Types(true).That().ResideInNamespaceOrBelow(AzureStorageNamespace).As("Azure Storage types");

    private static readonly IObjectProvider<IType> ChangeRecordType = Types(true).That().HaveFullName(typeof(ChangeRecord).FullName!).As("the change records");

    private static readonly IObjectProvider<IType> PersistenceAndAuditingTypes = Types().That().ResideInNamespaceMatching(@"^Dewiride\.Erp\.BuildingBlocks\.(Persistence|Auditing)(\..*)?$").As("persistence and auditing types");

    private static readonly IObjectProvider<IType> ModuleTypes = Types().That().ResideInNamespaceOrBelow("Dewiride.Erp.Modules").As("module types");

    private static readonly IObjectProvider<IType> SqlClientTypes = Types(true).That().ResideInNamespaceOrBelow("Microsoft.Data.SqlClient").As("SQL Server driver types");

    private static readonly IObjectProvider<IType> AdoNetTypes = Types(true).That().ResideInNamespaceMatching(@"^System\.Data\.Common$").As("ADO.NET provider types");

    private static readonly string[] ContextTypes = ["Microsoft.EntityFrameworkCore.DbContext", "Microsoft.EntityFrameworkCore.DbSet`1"];

    private static readonly string[] ChangeTrackerBypasses = ["Update", "UpdateRange", "Attach", "AttachRange"];

    private static readonly string[] TrackingTypes = [typeof(EntityEntry).FullName!, typeof(EntityEntry<>).FullName!, typeof(ChangeTracker).FullName!];

    private static readonly string[] TrackingBypasses = [StateSetter, nameof(ChangeTracker.TrackGraph)];

    private static readonly string[] RawSqlTypes = ["Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions"];

    private static readonly string[] RawSqlMethods = ["ExecuteSql", "SqlQuery", "FromSql", nameof(RelationalDatabaseFacadeExtensions.GetDbConnection)];

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
    public void Persistence_DoesNotDependOnAspNetCoreOrEndpoints()
    {
        AssertRule(Types().That().Are(PersistenceTypes).Should().NotDependOnAny(AspNetCoreTypes).AndShould().NotDependOnAny(EndpointTypes).WithoutRequiringPositiveResults());
    }

    [Fact]
    public void Hosting_DoesNotDependOnAspNetCoreEntityFrameworkOrEndpoints()
    {
        AssertRule(Types().That().Are(HostingTypes).Should().NotDependOnAny(AspNetCoreTypes).AndShould().NotDependOnAny(EntityFrameworkTypes).AndShould().NotDependOnAny(EndpointTypes).WithoutRequiringPositiveResults());
    }

    [Fact]
    public void AzureStorage_IsReferencedOnlyByTheBlobStorageNamespace()
    {
        Assert.NotEmpty(Types(true).That().ResideInNamespaceOrBelow(AzureStorageNamespace).GetObjects(Architecture));
        AssertRule(Types().That().AreNot(BlobStorageTypes).Should().NotDependOnAny(AzureStorageTypes));
    }

    [Fact]
    public void MicrosoftIdentityWeb_IsReferencedOnlyByTheAuthenticationBuildingBlock()
    {
        var referencing = ErpAssemblies.All
            .Where(assembly => assembly.GetReferencedAssemblies().Any(reference => reference.Name!.StartsWith(IdentityWebAssemblyPrefix, StringComparison.Ordinal)))
            .Select(assembly => assembly.GetName().Name)
            .ToList();

        Assert.Equal([AuthenticationAssembly], referencing);
    }

    [Theory]
    [InlineData("Azure.Security.KeyVault.Keys", AuthenticationAssembly)]
    [InlineData("Azure.Extensions.AspNetCore.DataProtection.Keys", AuthenticationAssembly)]
    [InlineData("Microsoft.Extensions.Caching.SqlServer", CachingAssembly)]
    public void KeyRingAndTokenCacheLibraries_AreReferencedOnlyByTheirBuildingBlock(string library, string owner)
    {
        var referencing = ErpAssemblies.All
            .Where(assembly => assembly.GetReferencedAssemblies().Any(reference => string.Equals(reference.Name, library, StringComparison.Ordinal)))
            .Select(assembly => assembly.GetName().Name)
            .ToList();

        Assert.Equal([owner], referencing);
    }

    [Fact]
    public void ProductAssemblies_NeverReferenceTheTestingLibrary()
    {
        var violations = ErpAssemblies.All
            .SelectMany(assembly => assembly.GetReferencedAssemblies().Select(reference => (Assembly: assembly.GetName().Name, Referenced: reference.Name!)))
            .Where(pair => pair.Referenced.StartsWith(TestingAssemblyPrefix, StringComparison.Ordinal))
            .Select(pair => $"{pair.Assembly} -> {pair.Referenced}")
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Handlers_AreSealed()
    {
        AssertRule(Classes().That().ImplementInterface(typeof(BuildingBlocks.Application.Commands.ICommandHandler<,>))
            .Or().ImplementInterface(typeof(BuildingBlocks.Application.Queries.IQueryHandler<,>))
            .Should().BeSealed().WithoutRequiringPositiveResults());
    }

    [Fact]
    public void ChangeRecords_AreReferencedOnlyByThePersistenceAndAuditingBuildingBlocks()
    {
        Assert.NotEmpty(Types().That().DependOnAny(ChangeRecordType).GetObjects(Architecture));
        AssertRule(Types().That().AreNot(PersistenceAndAuditingTypes).Should().NotDependOnAny(ChangeRecordType));
    }

    [Fact]
    public void ProductCode_NeverAttachesOrUpdatesEntitiesItDidNotRead()
    {
        var calls = EntityFrameworkCalls(ContextTypes).ToList();

        Assert.Contains(calls, call => call.Method is "Add" or "Remove");
        Assert.Empty(calls.Where(call => ChangeTrackerBypasses.Contains(call.Method)).Select(call => $"{call.Caller} calls {call.Target}"));
    }

    [Fact]
    public void Modules_NeverSendSqlOfTheirOwn()
    {
        var calls = EntityFrameworkCalls(RawSqlTypes).Where(call => RawSqlMethods.Any(prefix => call.Method.StartsWith(prefix, StringComparison.Ordinal))).ToList();

        Assert.Contains(calls, call => call.Caller.StartsWith(ErpAssemblies.BuildingBlocksPrefix, StringComparison.Ordinal));
        Assert.Empty(calls.Where(call => call.Caller.StartsWith(ErpAssemblies.ModulesPrefix, StringComparison.Ordinal)).Select(call => $"{call.Caller} calls {call.Target}"));
    }

    [Fact]
    public void Modules_NeverTrackEntitiesTheyDidNotRead()
    {
        var calls = EntityFrameworkCalls(TrackingTypes).ToList();

        Assert.All(TrackingTypes, declaringType => Assert.Contains(calls, call => call.DeclaringType == declaringType));
        Assert.Contains(calls, call => call.Method == StateSetter && call.Caller.StartsWith(ErpAssemblies.BuildingBlocksPrefix, StringComparison.Ordinal));
        Assert.Empty(calls
            .Where(call => call.Caller.StartsWith(ErpAssemblies.ModulesPrefix, StringComparison.Ordinal) && TrackingBypasses.Contains(call.Method))
            .Select(call => $"{call.Caller} calls {call.Target}"));
    }

    [Fact]
    public void Modules_NeverUseTheDatabaseDriver()
    {
        Assert.NotEmpty(Types().That().ResideInNamespaceOrBelow(BuildingBlocksNamespace).And().DependOnAny(SqlClientTypes).GetObjects(Architecture));
        Assert.NotEmpty(Types().That().ResideInNamespaceOrBelow(BuildingBlocksNamespace).And().DependOnAny(AdoNetTypes).GetObjects(Architecture));
        AssertRule(Types().That().Are(ModuleTypes).Should().NotDependOnAny(SqlClientTypes).AndShould().NotDependOnAny(AdoNetTypes));
    }

    private static IEnumerable<(string Caller, string DeclaringType, string Method, string Target)> EntityFrameworkCalls(IReadOnlyCollection<string> declaringTypes) =>
        Architecture.Types
            .SelectMany(type => type.Dependencies.OfType<MethodCallDependency>().Select(call => (Caller: type.FullName, call.TargetMember)))
            .Where(call => declaringTypes.Contains(call.TargetMember.DeclaringType.FullName))
            .Select(call => (call.Caller, DeclaringType: call.TargetMember.DeclaringType.FullName, Method: MethodName(call.TargetMember.Name), Target: call.TargetMember.FullName))
            .Distinct();

    private static string MethodName(string member)
    {
        var end = member.IndexOfAny(['(', '<', '`']);

        return end < 0 ? member : member[..end];
    }

    private static void AssertRule(IArchRule rule)
    {
        var failures = rule.Evaluate(Architecture).Where(r => !r.Passed).Select(r => r.Description).ToList();
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
