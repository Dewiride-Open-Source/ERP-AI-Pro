using Dewiride.Erp.ArchitectureTests.Rules.Probes.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Catalog;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using HostModules = Dewiride.Erp.Host.Composition.Modules;

namespace Dewiride.Erp.ArchitectureTests.Rules;

public sealed class PersistenceTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string PersistenceNamespaceSuffix = ".Persistence";

    [Fact]
    public void ModuleDbContexts_DeriveFromModuleDbContextInsideThePersistenceNamespace()
    {
        var violations = ErpAssemblies.ModuleImplementations
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(DbContext).IsAssignableFrom(t) && t is { IsAbstract: false })
            .Where(t => !typeof(ModuleDbContext).IsAssignableFrom(t) || t.Namespace != t.Assembly.GetName().Name + PersistenceNamespaceSuffix)
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void CatalogueContexts_MapOnlyEntityTypesFromTheirOwnAssemblyTheChangeRecordsAndTheIntegrationEventTables()
    {
        using var scope = factory.Services.CreateScope();

        var violations = Catalogue().Registrations
            .SelectMany(registration => ForeignEntityTypes(((DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType)).Model, registration.ContextType))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ForeignEntityTypes_ProbeContexts_AcceptTheSharedTablesAndRefuseAnotherAssemblysType()
    {
        using var recording = new ProbeContexts.Recording();
        using var foreign = new ProbeContexts.Foreign();

        Assert.Empty(ForeignEntityTypes(recording.Model, typeof(ProbeContexts.Recording)));
        Assert.Contains(ProbeContexts.Foreign.BagName, Assert.Single(ForeignEntityTypes(foreign.Model, typeof(ProbeContexts.Foreign))), StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleContexts_UseTheSchemaOfTheirDescriptor()
    {
        using var scope = factory.Services.CreateScope();
        var registrations = Catalogue().Registrations;

        foreach (var module in HostModules.All.Where(m => m.Descriptor.Schema is not null))
        {
            var registration = Assert.Single(registrations, r => r.ContextType.Assembly == module.GetType().Assembly);
            var context = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType);
            Assert.Equal(module.Descriptor.Schema, registration.Schema);
            Assert.Equal(module.Descriptor.Schema, context.Schema);
            Assert.Equal(module.Descriptor.Schema, context.Model.GetDefaultSchema());
        }
    }

    [Fact]
    public void CatalogueContexts_StronglyTypedIdProperties_UseTheConverter()
    {
        using var scope = factory.Services.CreateScope();
        var violations = new List<string>();

        foreach (var registration in Catalogue().Registrations)
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType);
            violations.AddRange(context.Model.GetEntityTypes()
                .SelectMany(e => e.GetProperties())
                .Where(p => StronglyTypedIdTypes.IsStronglyTypedId(Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType))
                .Where(p => p.GetValueConverter()?.GetType().GetGenericTypeDefinition() != typeof(StronglyTypedIdConverter<>))
                .Select(p => $"{p.DeclaringType.DisplayName()}.{p.Name}"));
        }

        Assert.Empty(violations);
    }

    private static List<string> ForeignEntityTypes(IModel model, Type contextType) =>
        [.. model.GetEntityTypes()
            .Where(e => e.ClrType.Assembly != contextType.Assembly && e.ClrType != typeof(ChangeRecord) && e.ClrType != typeof(OutboxMessage) && e.ClrType != typeof(ProcessedMessage))
            .Select(e => $"{contextType.Name} maps {e.DisplayName()} from {e.ClrType.Assembly.GetName().Name}")];

    private DbContextCatalog Catalogue() => factory.Services.GetRequiredService<DbContextCatalog>();
}
