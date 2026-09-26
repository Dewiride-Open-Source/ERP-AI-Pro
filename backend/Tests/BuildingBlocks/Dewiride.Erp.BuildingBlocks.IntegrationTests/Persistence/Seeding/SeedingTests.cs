using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;
using Dewiride.Erp.Testing.Sql;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Seeding;

public sealed class SeedingTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    [Fact]
    public async Task RunAsync_Twice_LeavesTheSameRows()
    {
        var run = Guid.CreateVersion7().ToString("N")[^12..];
        var names = new SampleSeedNames([$"seed-{run}-a", $"seed-{run}-b", $"seed-{run}-c"]);
        await using var provider = BuildProvider(names);
        var runner = provider.GetRequiredService<SeedRunner>();

        await runner.RunAsync(TestContext.Current.CancellationToken);
        var first = await ReadSeededAsync(names);
        await runner.RunAsync(TestContext.Current.CancellationToken);
        var second = await ReadSeededAsync(names);

        Assert.Equal(names.Names.Order(StringComparer.Ordinal), first.Select(row => row.Name));
        Assert.Equal(first, second);
    }

    private static ServiceProvider BuildProvider(SampleSeedNames names)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"{DatabaseOptions.SectionName}:ConnectionString"] = SqlTestDatabase.Current.ConnectionString })
            .Build());
        services.AddLogging();
        services.AddErpPersistenceCore();
        services.AddModuleDbContext<SampleDbContext>(SampleDbContext.SchemaName);
        services.AddSingleton(names);
        services.AddSeeder<SampleSeeder>(order: 10);

        return services.BuildServiceProvider();
    }

    private async Task<List<SeededRow>> ReadSeededAsync(SampleSeedNames names)
    {
        await using var scope = database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Samples.IncludeDeleted()
            .Where(sample => names.Names.Contains(sample.Name))
            .OrderBy(sample => sample.Name)
            .Select(sample => new SeededRow(sample.Id.Value, sample.Name, sample.CreatedAt))
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private sealed record SeededRow(Guid Id, string Name, DateTimeOffset CreatedAt);
}
