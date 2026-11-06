using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Relay;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Integration.Consumers;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Shipments;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents;

[Collection(IntegrationEventPublishers.Name)]
public sealed class RelayTests
{
    [Fact]
    public async Task RelayOneAsync_EventWithTwoSubscribers_EnqueuesOneDeliveryEachUnderTheEventsKeyAndMarksIt()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (_, id) = await events.DispatchAsync();

        await events.RelayAsync(id);

        var deliveries = await events.DeliveriesAsync(id);
        Assert.Equal(new[] { events.Printer, events.Recorder }.Order(StringComparer.Ordinal), deliveries.Select(run => run.JobName));
        Assert.All(deliveries, run => Assert.Equal((JobRunStatus.Pending, OutboxRelay.KeyOf(id)), (run.Status, run.IdempotencyKey)));
        Assert.Equal(events.FirstClock.GetUtcNow(), (await events.OutboxRowAsync(id))!.RelayedAt);
    }

    [Fact]
    public async Task RelayOneAsync_EventWithoutSubscribers_MarksItWithoutARun()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (_, id) = await events.DispatchAsync();
        await events.QueryAsync((context, token) => context.OutboxMessages.Where(m => m.Id == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.EventName, "test.events.nobody-listens"), token));

        await events.RelayAsync(id);

        Assert.Empty(await events.DeliveriesAsync(id));
        Assert.NotNull((await events.OutboxRowAsync(id))!.RelayedAt);
    }

    [Fact]
    public async Task RelayOneAsync_StoppedBetweenTheEnqueueAndTheMark_MakesNoSecondDelivery()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (_, id) = await events.DispatchAsync();
        await events.RelayAsync(id);
        var first = await events.DeliveriesAsync(id);
        await events.QueryAsync((context, token) => context.OutboxMessages.Where(m => m.Id == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.RelayedAt, (DateTimeOffset?)null), token));

        await IntegrationEventsDatabase.RelayAsync(events.Second, id);

        Assert.Equal(first.Select(run => run.Id), (await events.DeliveriesAsync(id)).Select(run => run.Id));
        Assert.NotNull((await events.OutboxRowAsync(id))!.RelayedAt);
    }

    [Fact]
    public async Task RelayOneAsync_TwoRunnersAtOnce_MakeOneDeliveryPerSubscriber()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (_, id) = await events.DispatchAsync();

        await Task.WhenAll(IntegrationEventsDatabase.RelayAsync(events.First, id), IntegrationEventsDatabase.RelayAsync(events.Second, id));

        Assert.Equal(2, (await events.DeliveriesAsync(id)).Count);
        Assert.NotNull((await events.OutboxRowAsync(id))!.RelayedAt);
    }

    [Fact]
    public async Task EnqueueDueAsync_EventAnotherProcessCommitted_WaitsForTheFullPassInsteadOfBeingReadAtEveryPoll()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var relay = events.First.Services.GetRequiredService<OutboxRelay>();
        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);
        var (_, id) = await events.DispatchAsync(host: events.Second);

        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);
        var beforeTheFullPass = (await events.OutboxRowAsync(id))!.RelayedAt;
        events.FirstClock.Advance(OutboxRelay.FullPassInterval);
        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);

        Assert.Null(beforeTheFullPass);
        Assert.NotNull((await events.OutboxRowAsync(id))!.RelayedAt);
        Assert.Equal(2, (await events.DeliveriesAsync(id)).Count);
    }

    [Fact]
    public async Task EnqueueDueAsync_EventThatCannotBePassedOn_LogsItOnceAndPassesOnTheEventsBehindIt()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (_, stuck) = await events.DispatchAsync();
        events.Advance(TimeSpan.FromSeconds(1));
        var (_, behind) = await events.DispatchAsync();
        var other = await events.DeliveryOf(stuck);
        using var otherEvent = JsonDocument.Parse($$"""{"shipmentId":"{{SampleShipmentId.Create().Value}}","parcels":9,"dispatchedAt":"2026-11-01T06:30:00+00:00"}""");
        await events.EnqueueAsync<ShipmentReceiptRecorder>(other with { Event = otherEvent.RootElement.Clone() }, OutboxRelay.KeyOf(stuck));
        var relay = events.First.Services.GetRequiredService<OutboxRelay>();

        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);
        relay.Announce();
        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);

        Assert.Null((await events.OutboxRowAsync(stuck))!.RelayedAt);
        Assert.NotNull((await events.OutboxRowAsync(behind))!.RelayedAt);
        Assert.Equal(2, (await events.DeliveriesAsync(behind)).Count);
        var failures = IntegrationEventsDatabase.LogsOf(events.First).GetSnapshot().Where(record => record.Message.StartsWith("Could not pass on", StringComparison.Ordinal) && record.Message.Contains(stuck.Value.ToString(), StringComparison.Ordinal)).ToList();
        Assert.Equal(LogLevel.Error, Assert.Single(failures).Level);
        Assert.IsType<InvalidOperationException>(failures[0].Exception);
    }
}
