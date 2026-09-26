using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Seeding;

public sealed class SeedRunnerTests
{
    [Fact]
    public void Seeders_Registered_AreOrderedByOrderThenByTypeName()
    {
        using var provider = Build(services => services
            .AddSeeder<ZuluSeeder>(order: 10)
            .AddSeeder<LateSeeder>(order: 20)
            .AddSeeder<AlphaSeeder>(order: 10));

        var order = provider.GetRequiredService<SeedRunner>().Seeders.Select(registration => registration.SeederType);

        Assert.Equal([typeof(AlphaSeeder), typeof(ZuluSeeder), typeof(LateSeeder)], order);
    }

    [Fact]
    public void Constructor_SameSeederRegisteredTwice_Throws()
    {
        using var provider = Build(services => services.AddSeeder<AlphaSeeder>(order: 10).AddSeeder<AlphaSeeder>(order: 30));

        var failure = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<SeedRunner>());

        Assert.Contains(nameof(AlphaSeeder), failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Seeders_RunInOrderEachInItsOwnScope()
    {
        using var provider = Build(services => services
            .AddSeeder<LateSeeder>(order: 20)
            .AddSeeder<AlphaSeeder>(order: 10));

        await provider.GetRequiredService<SeedRunner>().RunAsync(TestContext.Current.CancellationToken);

        var runs = provider.GetRequiredService<RunLog>().Runs;
        Assert.Equal([nameof(AlphaSeeder), nameof(LateSeeder)], runs.Select(run => run.Seeder));
        Assert.NotEqual(runs[0].ScopeId, runs[1].ScopeId);
    }

    private static ServiceProvider Build(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection().AddLogging().AddSingleton<RunLog>().AddScoped<ScopeMarker>().AddSingleton<SeedRunner>();
        register(services);

        return services.BuildServiceProvider();
    }

    private sealed class RunLog
    {
        public List<(string Seeder, Guid ScopeId)> Runs { get; } = [];
    }

    private sealed class ScopeMarker
    {
        public Guid Id { get; } = Guid.CreateVersion7();
    }

    private abstract class RecordingSeeder(RunLog log, ScopeMarker scope) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken)
        {
            log.Runs.Add((GetType().Name, scope.Id));
            return Task.CompletedTask;
        }
    }

    private sealed class AlphaSeeder(RunLog log, ScopeMarker scope) : RecordingSeeder(log, scope);

    private sealed class ZuluSeeder(RunLog log, ScopeMarker scope) : RecordingSeeder(log, scope);

    private sealed class LateSeeder(RunLog log, ScopeMarker scope) : RecordingSeeder(log, scope);
}
