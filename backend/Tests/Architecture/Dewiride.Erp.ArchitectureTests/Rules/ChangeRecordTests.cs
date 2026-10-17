using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Catalog;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using HostModules = Dewiride.Erp.Host.Composition.Modules;

namespace Dewiride.Erp.ArchitectureTests.Rules;

public sealed class ChangeRecordTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public void CatalogueContexts_MapTheChangeRecordsExcludedFromTheirMigrationsUnlessTheyOwnTheAuditSchema()
    {
        var owners = new List<string>();

        foreach (var (registration, model) in CatalogueModels(designTime: true))
        {
            var records = model.FindEntityType(typeof(ChangeRecord));
            Assert.True(records is not null, $"{registration.ContextType.Name} maps no change records");
            Assert.Equal(ChangeRecordMapping.Schema, records.GetSchema());
            Assert.Equal(ChangeRecordMapping.Table, records.GetTableName());
            Assert.Equal(registration.Schema != ChangeRecordMapping.Schema, records.IsTableExcludedFromMigrations());
            if (!records.IsTableExcludedFromMigrations())
            {
                owners.Add(registration.ContextType.Name);
            }
        }

        Assert.Single(owners);
    }

    [Fact]
    public void ModuleContexts_MapOnlyAuditableOrAppendOnlyEntityTypes()
    {
        var modules = CatalogueModels()
            .Where(pair => pair.Registration.ContextType.Assembly.GetName().Name!.StartsWith(ErpAssemblies.ModulesPrefix, StringComparison.Ordinal))
            .ToList();
        var violations = modules
            .SelectMany(pair => pair.Model.GetEntityTypes().Select(entityType => (pair.Registration, EntityType: entityType)))
            .Where(pair => !typeof(IAuditable).IsAssignableFrom(pair.EntityType.ClrType) && !typeof(IAppendOnly).IsAssignableFrom(pair.EntityType.ClrType))
            .Select(pair => $"{pair.Registration.ContextType.Name} maps {pair.EntityType.DisplayName()}, which is neither auditable nor append-only")
            .ToList();

        Assert.NotEmpty(modules);
        Assert.Empty(violations);
    }

    [Fact]
    public void RecordedEntityTypes_MeetEveryRuleOfTheCapture()
    {
        var violations = CatalogueModels()
            .SelectMany(pair => pair.Model.GetEntityTypes())
            .SelectMany(RecordedEntityTypes.ProblemsOf)
            .ToList();

        Assert.NotEmpty(RecordedTypes());
        Assert.Empty(violations);
    }

    [Fact]
    public void ChangeKeys_NameOneRecordedTypeEach()
    {
        var recorded = RecordedTypes();

        Assert.NotEmpty(recorded);
        Assert.Empty(SharedKeys(recorded.Select(pair => pair.EntityType)));
    }

    [Fact]
    public void SharedKeys_TwoRecordedTypesSharingAKey_AreReportedAndATypeDerivedFromARecordedTypeIsNot()
    {
        using var probe = new ProbeContext();

        var shared = Assert.Single(SharedKeys(RecordedTypesOf(probe)));

        Assert.StartsWith($"{ProbeContext.SharedKey} names ", shared, StringComparison.Ordinal);
        Assert.Contains(nameof(ProbeLedger), shared, StringComparison.Ordinal);
        Assert.Contains(nameof(ProbeJournal), shared, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleChangeKeys_BeginWithTheirModulesPermissionPrefix()
    {
        var recorded = RecordedTypes();
        var moduleTypes = new List<IEntityType>();
        var violations = new List<string>();

        foreach (var module in HostModules.All)
        {
            var own = recorded.Where(pair => pair.Registration.ContextType.Assembly == module.GetType().Assembly).Select(pair => pair.EntityType).ToList();
            moduleTypes.AddRange(own);
            violations.AddRange(KeysOutside(own, module.Descriptor.PermissionPrefix));
        }

        Assert.NotEmpty(moduleTypes);
        Assert.Empty(violations);
    }

    [Fact]
    public void KeysOutside_KeyOutsideTheModulesPermissionPrefix_IsReported()
    {
        using var probe = new ProbeContext();

        var outside = Assert.Single(KeysOutside(RecordedTypesOf(probe), ProbeContext.PermissionPrefix));

        Assert.Equal($"{nameof(ProbeStray)} records its changes as {ProbeContext.StrayKey}, outside {ProbeContext.PermissionPrefix}", outside);
    }

    [Fact]
    public void RecordNames_NameOnlyRecordedTypesAndEachTypeOnce()
    {
        var keys = RecordedTypes().Select(pair => RecordedEntityTypes.KeyOf(pair.EntityType)!).ToHashSet(StringComparer.Ordinal);
        using var scope = factory.Services.CreateScope();
        var names = scope.ServiceProvider.GetServices<IRecordNames>().ToList();

        Assert.NotEmpty(names.SelectMany(registration => registration.EntityTypes));
        Assert.Empty(NamingProblems(keys, names));
    }

    [Fact]
    public void NamingProblems_KeyNoTypeRecordsAndKeyNamedTwice_AreReported()
    {
        var problems = NamingProblems(
            new HashSet<string>([ProbeContext.SharedKey], StringComparer.Ordinal),
            [new ProbeNames(ProbeContext.SharedKey, ProbeContext.StrayKey), new ProbeNames(ProbeContext.SharedKey)]);

        Assert.Equal(
            [
                $"{nameof(ProbeNames)} names {ProbeContext.StrayKey}, which no recorded type declares",
                $"{ProbeContext.SharedKey} is named by {nameof(ProbeNames)} and {nameof(ProbeNames)}",
            ],
            problems);
    }

    private static List<string> SharedKeys(IEnumerable<IEntityType> recordedTypes) =>
        [.. recordedTypes
            .GroupBy(entityType => RecordedEntityTypes.KeyOf(entityType)!, StringComparer.Ordinal)
            .Where(group => group.Select(entityType => entityType.GetRootType().ClrType).Distinct().Count() > 1)
            .Select(group => $"{group.Key} names {string.Join(" and ", group.Select(entityType => entityType.DisplayName()))}")];

    private static List<string> KeysOutside(IEnumerable<IEntityType> recordedTypes, string permissionPrefix) =>
        [.. recordedTypes
            .Select(entityType => (EntityType: entityType, Key: RecordedEntityTypes.KeyOf(entityType)!))
            .Where(pair => !pair.Key.StartsWith(permissionPrefix, StringComparison.Ordinal))
            .Select(pair => $"{pair.EntityType.DisplayName()} records its changes as {pair.Key}, outside {permissionPrefix}")];

    private static List<string> NamingProblems(HashSet<string> recordedKeys, IEnumerable<IRecordNames> names)
    {
        var named = names.SelectMany(registration => registration.EntityTypes.Select(key => (Names: registration.GetType().Name, Key: key))).ToList();

        return
        [
            .. named.Where(pair => !recordedKeys.Contains(pair.Key)).Select(pair => $"{pair.Names} names {pair.Key}, which no recorded type declares"),
            .. named.GroupBy(pair => pair.Key, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => $"{group.Key} is named by {string.Join(" and ", group.Select(pair => pair.Names))}"),
        ];
    }

    private static IEnumerable<IEntityType> RecordedTypesOf(DbContext context) => context.Model.GetEntityTypes().Where(RecordedEntityTypes.IsRecorded);

    private List<(DbContextRegistration Registration, IEntityType EntityType)> RecordedTypes() =>
        [.. CatalogueModels()
            .SelectMany(pair => pair.Model.GetEntityTypes().Select(entityType => (pair.Registration, EntityType: entityType)))
            .Where(pair => RecordedEntityTypes.IsRecorded(pair.EntityType))];

    private List<(DbContextRegistration Registration, IModel Model)> CatalogueModels(bool designTime = false)
    {
        using var scope = factory.Services.CreateScope();
        var models = new List<(DbContextRegistration Registration, IModel Model)>();
        foreach (var registration in factory.Services.GetRequiredService<DbContextCatalog>().Registrations)
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType);
            models.Add((registration, designTime ? context.GetService<IDesignTimeModel>().Model : context.Model));
        }

        return models;
    }

    private sealed class ProbeContext() : ModuleDbContext(new DbContextOptionsBuilder<ProbeContext>().UseSqlServer().Options, "probe_ledgers")
    {
        public const string PermissionPrefix = "probe.ledgers.";

        public const string SharedKey = "probe.ledgers.entries";

        public const string StrayKey = "probe.other.strays";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProbeLedger>().RecordChangesAs(SharedKey).Property(l => l.Id).ValueGeneratedNever();
            modelBuilder.Entity<ProbeJournal>().RecordChangesAs(SharedKey).Property(j => j.Id).ValueGeneratedNever();
            modelBuilder.Entity<ProbeAccount>().RecordChangesAs("probe.ledgers.accounts").Property(a => a.Id).ValueGeneratedNever();
            modelBuilder.Entity<ProbeSavingsAccount>();
            modelBuilder.Entity<ProbeStray>().RecordChangesAs(StrayKey).Property(s => s.Id).ValueGeneratedNever();
            base.OnModelCreating(modelBuilder);
        }
    }

    private abstract class ProbeAudited : IAuditable
    {
        public Guid Id { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public Guid CreatedBy { get; set; }

        public DateTimeOffset? ModifiedAt { get; set; }

        public Guid? ModifiedBy { get; set; }
    }

    private sealed class ProbeLedger : ProbeAudited;

    private sealed class ProbeJournal : ProbeAudited;

    private class ProbeAccount : ProbeAudited;

    private sealed class ProbeSavingsAccount : ProbeAccount;

    private sealed class ProbeStray : ProbeAudited;

    private sealed class ProbeNames(params string[] entityTypes) : IRecordNames
    {
        public IReadOnlySet<string> EntityTypes { get; } = entityTypes.ToHashSet(StringComparer.Ordinal);

        public Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
    }
}
