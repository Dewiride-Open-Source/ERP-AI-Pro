using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Migrations;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.Testing.Sql;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

public sealed class SampleDatabase : IAsyncLifetime
{
    private ServiceProvider? _provider;

    public string ConnectionString => SqlTestDatabase.Current.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"{DatabaseOptions.SectionName}:ConnectionString"] = ConnectionString })
            .Build());
        services.AddLogging();
        services.AddErpPersistenceCore();
        services.AddModuleDbContext<SampleDbContext>(SampleDbContext.SchemaName);
        _provider = services.BuildServiceProvider();

        await _provider.GetRequiredService<DatabaseMigrator>().MigrateAllAsync(CancellationToken.None);
    }

    public AsyncServiceScope CreateScope() =>
        (_provider ?? throw new InvalidOperationException("The sample database has not been initialised.")).CreateAsyncScope();

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }
    }
}
