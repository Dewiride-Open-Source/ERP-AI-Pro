using Dewiride.Erp.BuildingBlocks.IntegrationEvents;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Retention;
using Dewiride.Erp.BuildingBlocks.Jobs;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents;

[Collection(IntegrationEventPublishers.Name)]
public sealed class RetentionTests
{
    private static readonly TimeSpan Tick = TimeSpan.FromTicks(1);

    [Fact]
    public async Task SweepAsync_PassedOnEvents_LeaveOnceOlderThanTheRetentionAndAWaitingOneNever()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (_, waiting) = await events.DispatchAsync();
        var (_, passedOn) = await events.DispatchAsync();
        await events.RelayAsync(passedOn);
        events.Advance(new IntegrationEventsOptions().PublishedEventRetention);

        await SweepAsync(events);
        var keptAtTheCutoff = await events.OutboxRowAsync(passedOn);
        events.Advance(Tick);
        await SweepAsync(events);

        Assert.NotNull(keptAtTheCutoff);
        Assert.Null(await events.OutboxRowAsync(passedOn));
        Assert.NotNull(await events.OutboxRowAsync(waiting));
    }

    [Fact]
    public async Task SweepAsync_ProcessedRecords_LeaveOnceOlderThanTheRunRetention()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (_, id) = await events.DispatchAsync();
        await events.RelayAsync(id);
        await events.RunAsync((await events.DeliveryAsync(id, events.Recorder)).Id);
        events.Advance(new JobsOptions().FinishedRunRetention);

        await SweepAsync(events);
        var keptAtTheCutoff = await events.ProcessedAsync(id);
        events.Advance(Tick);
        await SweepAsync(events);

        Assert.Single(keptAtTheCutoff);
        Assert.Empty(await events.ProcessedAsync(id));
    }

    private static async Task SweepAsync(IntegrationEventsDatabase events)
    {
        await using var scope = events.First.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IntegrationEventRetentionSweep>().SweepAsync(TestContext.Current.CancellationToken);
    }
}
