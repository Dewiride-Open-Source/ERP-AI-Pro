using Dewiride.Erp.BuildingBlocks.Persistence.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Retention;

public sealed class RetentionRunnerTests
{
    [Fact]
    public void Sweeps_Registered_AreOrderedByTypeName()
    {
        using var provider = Build(services => services
            .AddRetentionSweep<ZuluSweep>()
            .AddRetentionSweep<AlphaSweep>());

        var order = provider.GetRequiredService<RetentionRunner>().Sweeps.Select(registration => registration.SweepType);

        Assert.Equal([typeof(AlphaSweep), typeof(ZuluSweep)], order);
    }

    [Fact]
    public void Constructor_SameSweepRegisteredTwice_Throws()
    {
        using var provider = Build(services => services.AddRetentionSweep<AlphaSweep>().AddRetentionSweep<AlphaSweep>());

        var failure = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<RetentionRunner>());

        Assert.Contains(nameof(AlphaSweep), failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Sweeps_RunInOrderEachInItsOwnScopeAndAllComplete()
    {
        using var provider = Build(services => services.AddRetentionSweep<ZuluSweep>().AddRetentionSweep<AlphaSweep>());

        var outcome = await provider.GetRequiredService<RetentionRunner>().RunAsync(TestContext.Current.CancellationToken);

        var runs = provider.GetRequiredService<RunLog>().Runs;
        Assert.Equal([nameof(AlphaSweep), nameof(ZuluSweep)], runs.Select(run => run.Sweep));
        Assert.NotEqual(runs[0].ScopeId, runs[1].ScopeId);
        Assert.Equal(new RetentionOutcome(Completed: 2, Failed: 0), outcome);
    }

    [Fact]
    public async Task RunAsync_SweepThrows_LogsTheFailureAtErrorAndStillRunsTheNextSweep()
    {
        using var provider = Build(services => services.AddRetentionSweep<ZuluSweep>().AddRetentionSweep<FailingSweep>());

        var outcome = await provider.GetRequiredService<RetentionRunner>().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal([nameof(FailingSweep), nameof(ZuluSweep)], provider.GetRequiredService<RunLog>().Runs.Select(run => run.Sweep));
        Assert.Equal(new RetentionOutcome(Completed: 1, Failed: 1), outcome);
        var failure = Assert.Single(provider.GetRequiredService<FakeLogCollector>().GetSnapshot(), record => record.Level == LogLevel.Error);
        Assert.Equal(nameof(FailingSweep), failure.GetStructuredStateValue("Sweep"));
        Assert.IsType<InvalidOperationException>(failure.Exception);
    }

    [Fact]
    public async Task RunAsync_CancelledWhileASweepRuns_StopsWithoutRunningTheNextSweep()
    {
        using var cancellation = new CancellationTokenSource();
        using var provider = Build(services => services
            .AddSingleton(cancellation)
            .AddRetentionSweep<CancellingSweep>()
            .AddRetentionSweep<ZuluSweep>());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRequiredService<RetentionRunner>().RunAsync(cancellation.Token));

        Assert.Equal([nameof(CancellingSweep)], provider.GetRequiredService<RunLog>().Runs.Select(run => run.Sweep));
        Assert.DoesNotContain(provider.GetRequiredService<FakeLogCollector>().GetSnapshot(), record => record.Level == LogLevel.Error);
    }

    private static ServiceProvider Build(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection()
            .AddFakeLogging()
            .AddSingleton<RunLog>()
            .AddScoped<ScopeMarker>()
            .AddSingleton<RetentionRunner>();
        register(services);

        return services.BuildServiceProvider();
    }

    private sealed class RunLog
    {
        public List<(string Sweep, Guid ScopeId)> Runs { get; } = [];
    }

    private sealed class ScopeMarker
    {
        public Guid Id { get; } = Guid.CreateVersion7();
    }

    private abstract class RecordingSweep(RunLog log, ScopeMarker scope) : IRetentionSweep
    {
        public virtual Task SweepAsync(CancellationToken cancellationToken)
        {
            log.Runs.Add((GetType().Name, scope.Id));
            return Task.CompletedTask;
        }
    }

    private sealed class AlphaSweep(RunLog log, ScopeMarker scope) : RecordingSweep(log, scope);

    private sealed class ZuluSweep(RunLog log, ScopeMarker scope) : RecordingSweep(log, scope);

    private sealed class FailingSweep(RunLog log, ScopeMarker scope) : RecordingSweep(log, scope)
    {
        public override async Task SweepAsync(CancellationToken cancellationToken)
        {
            await base.SweepAsync(cancellationToken);
            throw new InvalidOperationException("The rows could not be deleted.");
        }
    }

    private sealed class CancellingSweep(RunLog log, ScopeMarker scope, CancellationTokenSource cancellation) : RecordingSweep(log, scope)
    {
        public override async Task SweepAsync(CancellationToken cancellationToken)
        {
            await base.SweepAsync(cancellationToken);
            await cancellation.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
