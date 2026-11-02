using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Jobs.Queue;

public sealed class JobSignalTests
{
    [Fact]
    public async Task WaitAsync_WokenBeforeTheWait_ReturnsAtOnceAndOnlyOnce()
    {
        var clock = new FakeTimeProvider();
        var signal = new JobSignal();

        signal.Wake();
        await signal.WaitAsync(TimeSpan.FromMinutes(1), clock, TestContext.Current.CancellationToken);
        var second = signal.WaitAsync(TimeSpan.FromMinutes(1), clock, TestContext.Current.CancellationToken);

        Assert.False(second.IsCompleted);
        signal.Wake();
        await second;
    }

    [Fact]
    public async Task WaitAsync_NoWake_EndsWhenTheIntervalPasses()
    {
        var clock = new FakeTimeProvider();
        var signal = new JobSignal();

        var wait = signal.WaitAsync(TimeSpan.FromSeconds(30), clock, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(wait.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));

        await wait;
    }

    [Fact]
    public async Task WaitAsync_Cancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var wait = new JobSignal().WaitAsync(TimeSpan.FromMinutes(1), new FakeTimeProvider(), cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }
}
