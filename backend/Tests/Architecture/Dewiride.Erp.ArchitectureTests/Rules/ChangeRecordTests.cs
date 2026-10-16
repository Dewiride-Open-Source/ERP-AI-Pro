using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
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
        var shared = RecordedTypes()
            .GroupBy(pair => RecordedEntityTypes.KeyOf(pair.EntityType)!, StringComparer.Ordinal)
            .Where(group => group.Select(pair => pair.EntityType.GetRootType().ClrType).Distinct().Count() > 1)
            .Select(group => $"{group.Key} names {string.Join(" and ", group.Select(pair => pair.EntityType.DisplayName()))}")
            .ToList();

        Assert.Empty(shared);
    }

    [Fact]
    public void ModuleChangeKeys_BeginWithTheirModulesPermissionPrefix()
    {
        var recorded = RecordedTypes();
        var violations = new List<string>();

        foreach (var module in HostModules.All)
        {
            violations.AddRange(recorded
                .Where(pair => pair.Registration.ContextType.Assembly == module.GetType().Assembly)
                .Select(pair => (pair.EntityType, Key: RecordedEntityTypes.KeyOf(pair.EntityType)!))
                .Where(pair => !pair.Key.StartsWith(module.Descriptor.PermissionPrefix, StringComparison.Ordinal))
                .Select(pair => $"{pair.EntityType.DisplayName()} records its changes as {pair.Key}, outside {module.Descriptor.PermissionPrefix}"));
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void RecordNames_NameOnlyRecordedTypesAndEachTypeOnce()
    {
        var keys = RecordedTypes().Select(pair => RecordedEntityTypes.KeyOf(pair.EntityType)!).ToHashSet(StringComparer.Ordinal);
        using var scope = factory.Services.CreateScope();
        var named = scope.ServiceProvider.GetServices<IRecordNames>()
            .SelectMany(names => names.EntityTypes.Select(key => (Names: names.GetType().Name, Key: key)))
            .ToList();

        Assert.Empty(named.Where(pair => !keys.Contains(pair.Key)).Select(pair => $"{pair.Names} names {pair.Key}, which no recorded type declares"));
        Assert.Empty(named.GroupBy(pair => pair.Key, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => $"{group.Key} is named by {string.Join(" and ", group.Select(pair => pair.Names))}"));
    }

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
}
