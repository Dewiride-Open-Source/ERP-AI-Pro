using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Retention;
using Microsoft.EntityFrameworkCore;

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
        events.Advance(events.EventRetention);

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
        events.Advance(events.RunRetention);

        await SweepAsync(events);
        var keptAtTheCutoff = await events.ProcessedAsync(id);
        events.Advance(Tick);
        await SweepAsync(events);

        Assert.Single(keptAtTheCutoff);
        Assert.Empty(await events.ProcessedAsync(id));
    }

    [Fact]
    public async Task SweepAsync_MoreRowsThanABatch_DeletesThemAll()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var ids = await events.DispatchManyAsync(IntegrationEventRetentionSweep.BatchSize + 1);
        var now = events.FirstClock.GetUtcNow();
        await events.QueryAsync((context, token) => context.OutboxMessages.Where(m => ids.Contains(m.Id)).ExecuteUpdateAsync(s => s.SetProperty(m => m.RelayedAt, now), token));
        await events.QueryAsync((context, token) =>
        {
            context.ProcessedMessages.AddRange(ids.Select(id => new ProcessedMessage(id, events.Recorder, now)));

            return context.SaveChangesAsync(token);
        });
        events.Advance(new[] { events.EventRetention, events.RunRetention }.Max() + Tick);

        await SweepAsync(events);

        Assert.Empty(await events.OutboxRowsAsync(ids));
        Assert.Equal(0, await events.QueryAsync((context, token) => context.ProcessedMessages.CountAsync(m => ids.Contains(m.EventId), token)));
    }

    private static async Task SweepAsync(IntegrationEventsDatabase events)
    {
        await using var scope = events.First.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IntegrationEventRetentionSweep>().SweepAsync(TestContext.Current.CancellationToken);
    }
}
