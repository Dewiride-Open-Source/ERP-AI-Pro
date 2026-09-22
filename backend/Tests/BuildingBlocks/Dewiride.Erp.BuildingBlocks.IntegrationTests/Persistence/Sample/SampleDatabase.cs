using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Idempotency.Persistence;
using Dewiride.Erp.BuildingBlocks.Idempotency.Storage;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Dewiride.Erp.BuildingBlocks.Persistence.Migrations;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.Testing.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

public sealed class SampleDatabase : IAsyncLifetime
{
    private ServiceProvider? _provider;

    public string ConnectionString => SqlTestDatabase.Current.ConnectionString;

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 9, 22, 6, 0, 0, TimeSpan.Zero));

    public TestActorContext Actor { get; } = new();

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"{DatabaseOptions.SectionName}:ConnectionString"] = ConnectionString })
            .Build());
        services.AddLogging();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped<IActorContext>(_ => Actor);
        services.AddErpPersistenceCore();
        services.AddModuleDbContext<SampleDbContext>(SampleDbContext.SchemaName);
        services.AddModuleDbContext<IdempotencyDbContext>(IdempotencyDbContext.SchemaName);
        services.AddScoped<IIdempotencyStore, SqlIdempotencyStore>();
        services.AddHandlersFromAssembly(typeof(SampleDatabase).Assembly);
        _provider = services.BuildServiceProvider();

        await _provider.GetRequiredService<DatabaseMigrator>().MigrateAllAsync(CancellationToken.None);
    }

    public IServiceScopeFactory ScopeFactory => Provider.GetRequiredService<IServiceScopeFactory>();

    private ServiceProvider Provider => _provider ?? throw new InvalidOperationException("The sample database has not been initialised.");

    public AsyncServiceScope CreateScope() => Provider.CreateAsyncScope();

    public async Task AddAsync(SampleAggregate sample)
    {
        await using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        context.Samples.Add(sample);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async Task<SampleAggregate> FindAsync(SampleId id, bool includeDeleted = false)
    {
        await using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var query = includeDeleted ? context.Samples.IncludeDeleted() : context.Samples;

        return await query.AsNoTracking().SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }
    }
}
