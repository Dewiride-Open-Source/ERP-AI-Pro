using System.Text.RegularExpressions;
using Dewiride.Erp.ArchitectureTests.Rules.Probes.Approvals;
using Dewiride.Erp.ArchitectureTests.Rules.Probes.Approvals.Integration;
using Dewiride.Erp.ArchitectureTests.Rules.Probes.Persistence;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Outcomes;
using Dewiride.Erp.BuildingBlocks.Approvals.Persistence;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Catalog;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using HostModules = Dewiride.Erp.Host.Composition.Modules;

namespace Dewiride.Erp.ArchitectureTests.Rules;

// A context that asks for approval maps the two approval tables as their configurations do, so its requests and the decisions on
// them meet in the same rows, and leaves them out of its migrations, which only ApprovalsDbContext's create; whether a table is left
// out of the migrations is kept in the design-time model alone, so the tables are read from it. A kind is its module's:
// its key and the record it approves are the module's, its requests are saved in the one context the module owns, which maps the
// tables, and its outcome handler is a sealed class of the module's Integration folder that a scope of the API host builds, as a
// decision builds it. No product module declares a kind yet, so every rule of a kind runs over the product's kinds, where it must
// find nothing, and over the probes of Rules/Probes/Approvals, where it must accept the probe that keeps it and refuse each that
// breaks it.
public sealed partial class ApprovalTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private static readonly (Type Type, string Table)[] Tables = [(typeof(ApprovalRequest), ApprovalTables.Requests), (typeof(ApprovalStep), ApprovalTables.Steps)];

    [Fact]
    public void CatalogueContexts_MapTheApprovalTablesAsTheirOwnerKeepsThem()
    {
        using var scope = factory.Services.CreateScope();
        var registrations = factory.Services.GetRequiredService<DbContextCatalog>().Registrations;

        var problems = registrations
            .SelectMany(registration => TableProblems(DesignTimeModelOf((DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType)), registration.ContextType))
            .ToList();

        Assert.Empty(problems);
        Assert.Contains(registrations, registration => registration.ContextType == typeof(ApprovalsDbContext));
    }

    [Fact]
    public void TableProblems_AContextMappingTheTablesAsConfigured_HasNone()
    {
        using var context = new ProbeContexts.Approving();

        Assert.Empty(TableProblems(DesignTimeModelOf(context), typeof(ProbeContexts.Approving)));
    }

    [Fact]
    public void TableProblems_AContextThatRenamesATable_IsReported()
    {
        using var context = new ProbeContexts.Reapproving();

        Assert.Equal([$"{nameof(ProbeContexts.Reapproving)} maps {nameof(ApprovalStep)} to probe_reapproving.Steps, not {ApprovalTables.Schema}.{ApprovalTables.Steps}"], TableProblems(DesignTimeModelOf(context), typeof(ProbeContexts.Reapproving)));
    }

    [Fact]
    public void TableProblems_AContextThatWouldCreateATable_IsReported()
    {
        using var context = new ProbeContexts.ApprovalsMigrating();

        Assert.Equal([$"{nameof(ProbeContexts.ApprovalsMigrating)} creates {ApprovalTables.Schema}.{ApprovalTables.Requests}, which only {nameof(ApprovalsDbContext)}'s migrations create"], TableProblems(DesignTimeModelOf(context), typeof(ProbeContexts.ApprovalsMigrating)));
    }

    [Fact]
    public void Kinds_OfTheApiHost_AreEachTheirModulesAndSavedInTheirModulesContext()
    {
        using var scope = factory.Services.CreateScope();
        var catalog = factory.Services.GetRequiredService<DbContextCatalog>();
        var kinds = factory.Services.GetRequiredService<ApprovalKindCatalog>().Kinds;

        var problems = KindProblems(
            kinds,
            handler => HostModules.All.SingleOrDefault(module => module.GetType().Assembly == handler.Assembly)?.Descriptor.PermissionPrefix,
            handler => catalog.OwnedBy(handler.Assembly)?.ContextType,
            context => ((DbContext)scope.ServiceProvider.GetRequiredService(context)).Model);

        Assert.Empty(problems);
    }

    [Fact]
    public void KindProblems_ProbeKinds_AcceptTheKindThatKeepsTheRulesAndRefuseEachThatBreaksOne()
    {
        using var approving = new ProbeContexts.Approving();
        using var recording = new ProbeContexts.Recording();
        IModel? ModelOf(Type context) => context == typeof(ProbeContexts.Approving) ? approving.Model : context == typeof(ProbeContexts.Recording) ? recording.Model : null;
        List<string> ProblemsOf(ApprovalKind kind, Type context, Type handler) => KindProblems([new ApprovalKindRegistration(kind, context, handler)], _ => ProbeApprovals.PermissionPrefix, _ => typeof(ProbeContexts.Approving), ModelOf);

        Assert.Empty(ProblemsOf(ProbeApprovals.Keeping, typeof(ProbeContexts.Approving), typeof(ProbeOutcomes.ClaimOutcomes)));
        Assert.Contains(ProblemsOf(ProbeApprovals.Stray, typeof(ProbeContexts.Approving), typeof(ProbeOutcomes.ClaimOutcomes)), problem => problem.Contains("outside its module's prefix", StringComparison.Ordinal));
        Assert.Contains(ProblemsOf(ProbeApprovals.Rambling, typeof(ProbeContexts.Approving), typeof(ProbeOutcomes.ClaimOutcomes)), problem => problem.Contains("not described in one sentence", StringComparison.Ordinal));
        Assert.Contains(ProblemsOf(ProbeApprovals.Unrecorded, typeof(ProbeContexts.Approving), typeof(ProbeOutcomes.ClaimOutcomes)), problem => problem.Contains("which its module records as no type", StringComparison.Ordinal));
        Assert.Contains(ProblemsOf(ProbeApprovals.Keeping, typeof(ProbeContexts.Recording), typeof(ProbeOutcomes.ClaimOutcomes)), problem => problem.Contains("not in the context its module owns", StringComparison.Ordinal));
        Assert.Contains(ProblemsOf(ProbeApprovals.Keeping, typeof(ProbeContexts.Recording), typeof(ProbeOutcomes.ClaimOutcomes)), problem => problem.Contains("maps no approvals", StringComparison.Ordinal));
        Assert.Contains(ProblemsOf(ProbeApprovals.Keeping, typeof(ProbeContexts.Approving), typeof(ProbeOutcomes.OpenClaimOutcomes)), problem => problem.Contains("not a sealed class of an Integration namespace", StringComparison.Ordinal));
        Assert.Contains(ProblemsOf(ProbeApprovals.Keeping, typeof(ProbeContexts.Approving), typeof(MisplacedOutcomes)), problem => problem.Contains("not a sealed class of an Integration namespace", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handlers_OfTheApiHostsKinds_CanEachBeBuiltInAScopeOfTheApiHost()
    {
        var kinds = factory.Services.GetRequiredService<ApprovalKindCatalog>().Kinds;

        Assert.Empty(await ActivationProblemsAsync(kinds.Select(registration => registration.HandlerType)));
    }

    [Fact]
    public async Task ActivationProblems_ProbeHandlers_AcceptAHandlerTheHostCanBuildAndRefuseOneItCannot()
    {
        Assert.Empty(await ActivationProblemsAsync([typeof(ProbeOutcomes.ClaimOutcomes)]));
        Assert.Contains(typeof(ProbeOutcomes.LedgerClaimOutcomes).FullName!, Assert.Single(await ActivationProblemsAsync([typeof(ProbeOutcomes.LedgerClaimOutcomes)])), StringComparison.Ordinal);
    }

    private static IModel DesignTimeModelOf(DbContext context) => context.GetService<IDesignTimeModel>().Model;

    private static List<string> TableProblems(IModel model, Type contextType)
    {
        var problems = new List<string>();
        foreach (var (type, table) in Tables)
        {
            if (model.FindEntityType(type) is not { } entity)
            {
                continue;
            }

            if ((entity.GetSchema(), entity.GetTableName()) != (ApprovalTables.Schema, table))
            {
                problems.Add($"{contextType.Name} maps {type.Name} to {entity.GetSchema()}.{entity.GetTableName()}, not {ApprovalTables.Schema}.{table}");
            }
            else if (entity.IsTableExcludedFromMigrations() == (contextType == typeof(ApprovalsDbContext)))
            {
                problems.Add(contextType == typeof(ApprovalsDbContext)
                    ? $"{contextType.Name} leaves {ApprovalTables.Schema}.{table} out of its migrations, which create it"
                    : $"{contextType.Name} creates {ApprovalTables.Schema}.{table}, which only {nameof(ApprovalsDbContext)}'s migrations create");
            }
        }

        return problems;
    }

    private static List<string> KindProblems(IEnumerable<ApprovalKindRegistration> kinds, Func<Type, string?> prefixOf, Func<Type, Type?> contextOf, Func<Type, IModel?> modelOf)
    {
        var problems = new List<string>();
        foreach (var (kind, contextType, handlerType) in kinds)
        {
            if (prefixOf(handlerType) is not { } prefix || !kind.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                problems.Add($"The approval kind {kind.Key} is outside its module's prefix");
            }

            if (!OneSentence().IsMatch(kind.Description))
            {
                problems.Add($"The approval kind {kind.Key} is not described in one sentence");
            }

            if (contextOf(handlerType) != contextType)
            {
                problems.Add($"The approval kind {kind.Key} saves its requests in {contextType.Name}, not in the context its module owns");
            }

            var model = modelOf(contextType);
            if (model?.FindEntityType(typeof(ApprovalRequest)) is null || model.FindEntityType(typeof(ApprovalStep)) is null)
            {
                problems.Add($"{contextType.Name} maps no approvals, so the requests of {kind.Key} have nowhere to be saved");
            }

            if (model is null || !model.GetEntityTypes().Any(entity => entity.ClrType.Assembly == handlerType.Assembly && RecordedEntityTypes.IsRecorded(entity) && RecordedEntityTypes.KeyOf(entity) == kind.SubjectType))
            {
                problems.Add($"The approval kind {kind.Key} approves {kind.SubjectType}, which its module records as no type");
            }

            if (!handlerType.IsSealed || !(handlerType.Namespace?.EndsWith(".Integration", StringComparison.Ordinal) ?? false))
            {
                problems.Add($"{handlerType.FullName} is not a sealed class of an Integration namespace");
            }
        }

        return problems;
    }

    [GeneratedRegex(@"\A[A-Z][^.!?\r\n]*\.\z")]
    private static partial Regex OneSentence();

    private async Task<List<string>> ActivationProblemsAsync(IEnumerable<Type> handlers)
    {
        var problems = new List<string>();
        foreach (var handler in handlers)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            try
            {
                var built = ActivatorUtilities.CreateFactory(handler, Type.EmptyTypes)(scope.ServiceProvider, null);
                if (built is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (built is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (InvalidOperationException exception)
            {
                problems.Add($"{handler.FullName}: {exception.Message}");
            }
        }

        return problems;
    }

    public sealed class MisplacedOutcomes : IApprovalOutcomeHandler
    {
        public Task<Result> HandleAsync(ApprovalOutcome outcome, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    }
}
