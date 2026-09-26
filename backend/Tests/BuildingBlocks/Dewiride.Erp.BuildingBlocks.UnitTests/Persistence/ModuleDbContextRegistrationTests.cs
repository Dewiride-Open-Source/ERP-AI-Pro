using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Catalog;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence;

public sealed class ModuleDbContextRegistrationTests
{
    private const string Schema = "test_schema";

    private const string ConnectionString = "Server=localhost;Database=ErpAiPro;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";

    [Fact]
    public void Configure_SqlServerProvider_SetsHistoryTableTimeoutCompatibilityLevelAndRetry()
    {
        var builder = new DbContextOptionsBuilder<SampleContext>();
        var options = new DatabaseOptions { ConnectionString = ConnectionString, CommandTimeout = TimeSpan.FromSeconds(45), MaxRetryCount = 3, MaxRetryDelay = TimeSpan.FromSeconds(7) };

        ModuleDbContextRegistration.Configure(builder, options, Schema);

        var relational = Assert.Single(builder.Options.Extensions.OfType<RelationalOptionsExtension>());
        Assert.Equal(ModuleDbContextRegistration.MigrationsHistoryTable, relational.MigrationsHistoryTableName);
        Assert.Equal(Schema, relational.MigrationsHistoryTableSchema);
        Assert.Equal(45, relational.CommandTimeout);
        Assert.NotNull(relational.ExecutionStrategyFactory);
        Assert.Contains("EngineType=SqlServer", relational.Info.LogFragment, StringComparison.Ordinal);
        Assert.Contains($"CompatibilityLevel={ModuleDbContextRegistration.CompatibilityLevel}", relational.Info.LogFragment, StringComparison.Ordinal);
    }

    [Fact]
    public void Configure_AzureSqlProvider_UsesAzureSqlWithHistoryTableTimeoutAndRetry()
    {
        var builder = new DbContextOptionsBuilder<SampleContext>();
        var options = new DatabaseOptions { ConnectionString = ConnectionString, Provider = DatabaseProvider.AzureSql, CommandTimeout = TimeSpan.FromSeconds(20) };

        ModuleDbContextRegistration.Configure(builder, options, Schema);

        var relational = Assert.Single(builder.Options.Extensions.OfType<RelationalOptionsExtension>());
        Assert.Equal(ModuleDbContextRegistration.MigrationsHistoryTable, relational.MigrationsHistoryTableName);
        Assert.Equal(Schema, relational.MigrationsHistoryTableSchema);
        Assert.Equal(20, relational.CommandTimeout);
        Assert.NotNull(relational.ExecutionStrategyFactory);
        Assert.Contains("EngineType=AzureSql", relational.Info.LogFragment, StringComparison.Ordinal);
    }

    [Fact]
    public void Configure_ConnectionStringAbsent_IsAcceptedForDesignTime()
    {
        var builder = new DbContextOptionsBuilder<SampleContext>();

        ModuleDbContextRegistration.Configure(builder, new DatabaseOptions(), Schema);

        var relational = Assert.Single(builder.Options.Extensions.OfType<RelationalOptionsExtension>());
        Assert.Null(relational.ConnectionString);
    }

    [Fact]
    public void AddModuleDbContext_RegistersTheContextTheCatalogueEntryAndTheReadinessCheck()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"{DatabaseOptions.SectionName}:ConnectionString"] = ConnectionString })
            .Build());
        services.AddErpPersistenceCore();

        services.AddModuleDbContext<SampleContext>(Schema);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleContext>();
        Assert.Equal(Schema, context.Schema);
        var registration = Assert.Single(provider.GetRequiredService<DbContextCatalog>().Registrations);
        Assert.Equal(new DbContextRegistration(typeof(SampleContext), Schema), registration);
        var check = Assert.Single(provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);
        Assert.Equal($"database:{Schema}", check.Name);
        Assert.Contains("ready", check.Tags);
    }

    [Theory]
    [InlineData("InMemory", WarningBehavior.Log)]
    [InlineData("LocalDevelopment", WarningBehavior.Throw)]
    [InlineData(null, WarningBehavior.Throw)]
    public void AddModuleDbContext_ConfigurationSourceSetting_LogsTheManyServiceProvidersWarningOnlyInTestHostsAndThrowsElsewhere(string? source, WarningBehavior behavior)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:ConnectionString"] = ConnectionString,
                [ErpConfigurationSourceResolver.SourceSetting] = source,
            })
            .Build());
        services.AddErpPersistenceCore();
        services.AddModuleDbContext<SampleContext>(Schema);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<SampleContext>>();

        Assert.Equal(behavior, options.FindExtension<CoreOptionsExtension>()?.WarningsConfiguration.GetBehavior(CoreEventId.ManyServiceProvidersCreatedWarning));
    }

    private sealed class SampleContext(DbContextOptions<SampleContext> options) : ModuleDbContext(options, ModuleDbContextRegistrationTests.Schema);
}
