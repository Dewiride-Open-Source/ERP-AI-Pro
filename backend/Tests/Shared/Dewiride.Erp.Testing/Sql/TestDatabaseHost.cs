using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.Persistence.Migrations;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;
using Dewiride.Erp.Host.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Testing.Sql;

public static class TestDatabaseHost
{
    public static IHost Build(string connectionString, DatabaseProvider provider = DatabaseProvider.SqlServer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            EnvironmentName = Environments.Development,
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [ErpConfigurationSourceResolver.SourceSetting] = ErpConfigurationSourceResolver.InMemorySource,
            [$"{DatabaseOptions.SectionName}:ConnectionString"] = connectionString,
            [$"{DatabaseOptions.SectionName}:Provider"] = provider.ToString(),
        });
        builder.AddErpPlatform(typeof(Program).Assembly);

        return builder.Build();
    }

    public static async Task MigrateAsync(string connectionString)
    {
        using var host = Build(connectionString);
        await host.Services.GetRequiredService<DatabaseMigrator>().MigrateAllAsync(CancellationToken.None);
        await host.Services.GetRequiredService<SeedRunner>().RunAsync(CancellationToken.None);
    }
}
