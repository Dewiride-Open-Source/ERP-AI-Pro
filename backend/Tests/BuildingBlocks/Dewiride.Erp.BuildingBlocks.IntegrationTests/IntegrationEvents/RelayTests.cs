using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Relay;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Integration.Consumers;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Shipments;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
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
    public async Task EnqueueDueAsync_FirstPassAfterAStart_PassesOnAnEventNoAnnouncementReached()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (_, id) = await events.DispatchAsync(host: events.Second);

        await RelayOf(events).EnqueueDueAsync(TestContext.Current.CancellationToken);

        Assert.NotNull((await events.OutboxRowAsync(id))!.RelayedAt);
        Assert.Equal(2, (await events.DeliveriesAsync(id)).Count);
    }

    [Fact]
    public async Task EnqueueDueAsync_EventCommittedInThisProcessAfterAPass_IsPassedOnAtTheNextPoll()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var relay = RelayOf(events);
        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);
        var (_, id) = await events.DispatchAsync();

        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);

        Assert.NotNull((await events.OutboxRowAsync(id))!.RelayedAt);
        Assert.Equal(2, (await events.DeliveriesAsync(id)).Count);
    }

    [Fact]
    public async Task EnqueueDueAsync_EventAnotherProcessCommitted_WaitsForTheFullPassInsteadOfBeingReadAtEveryPoll()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var relay = RelayOf(events);
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
    public async Task EnqueueDueAsync_EventThatKeepsFailing_IsTriedAgainAtTheNextPollOnceAndThenHourlyWhileTheEventsBehindItGoOn()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (_, stuck) = await events.DispatchAsync();
        events.Advance(TimeSpan.FromSeconds(1));
        var (_, behind) = await events.DispatchAsync();
        var delivery = await events.DeliveryPayloadAsync(stuck);
        using var otherEvent = JsonDocument.Parse($$"""{"shipmentId":"{{SampleShipmentId.Create().Value}}","parcels":9,"dispatchedAt":"2026-11-01T06:30:00+00:00"}""");
        var conflicting = await events.EnqueueAsync<ShipmentReceiptRecorder>(delivery with { Event = otherEvent.RootElement.Clone() }, OutboxRelay.KeyOf(stuck));
        var relay = RelayOf(events);

        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);
        var behindAfterTheFirstPass = (await events.OutboxRowAsync(behind))!.RelayedAt;
        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);
        await events.QueryJobsAsync((context, token) => context.Runs.Where(r => r.Id == conflicting).ExecuteUpdateAsync(s => s.SetProperty(r => r.Payload, JobPayloads.Serialize(delivery)), token));
        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);
        var stuckBeforeTheFullPass = (await events.OutboxRowAsync(stuck))!.RelayedAt;
        events.Advance(OutboxRelay.FullPassInterval);
        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(behindAfterTheFirstPass);
        Assert.Equal(2, (await events.DeliveriesAsync(behind)).Count);
        Assert.Null(stuckBeforeTheFullPass);
        Assert.NotNull((await events.OutboxRowAsync(stuck))!.RelayedAt);
        Assert.Contains(conflicting, (await events.DeliveriesAsync(stuck)).Select(run => run.Id));
        var lines = IntegrationEventsDatabase.LogsOf(events.First).GetSnapshot().Where(record => record.Message.Contains(stuck.Value.ToString(), StringComparison.Ordinal)).ToList();
        var failure = Assert.Single(lines, record => record.Message.StartsWith("Could not pass on", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Error, failure.Level);
        Assert.IsType<InvalidOperationException>(failure.Exception);
        Assert.Single(lines, record => record.Message.EndsWith("after 2 failed passes", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EnqueueDueAsync_OutboxThatCannotBeRead_IsTriedAgainAtTheNextPollOnceAndThenHourly()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (_, id) = await events.DispatchAsync(host: events.Second);
        var relay = RelayOf(events);
        events.OutboxReads.FailEveryRead();

        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);
        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);
        events.OutboxReads.Stop();
        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);
        var beforeTheFullPass = (await events.OutboxRowAsync(id))!.RelayedAt;
        events.Advance(OutboxRelay.FullPassInterval);
        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);

        Assert.Null(beforeTheFullPass);
        Assert.NotNull((await events.OutboxRowAsync(id))!.RelayedAt);
        var lines = IntegrationEventsDatabase.LogsOf(events.First).GetSnapshot().Where(record => record.Message.Contains(nameof(SampleDbContext), StringComparison.Ordinal)).ToList();
        var failure = Assert.Single(lines, record => record.Message.StartsWith("Could not read the outbox", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Error, failure.Level);
        Assert.IsType<InvalidOperationException>(failure.Exception);
        Assert.Single(lines, record => record.Message.EndsWith("again after 2 failed passes", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EnqueueDueAsync_EventThatFailsFirstOnAPageBeforeTheOutboxFailsAgain_IsTriedAgainAtTheNextPoll()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var relay = RelayOf(events);
        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);
        var (_, stuck) = await events.DispatchAsync();
        var delivery = await events.DeliveryPayloadAsync(stuck);
        using var otherEvent = JsonDocument.Parse($$"""{"shipmentId":"{{SampleShipmentId.Create().Value}}","parcels":9,"dispatchedAt":"2026-11-01T06:30:00+00:00"}""");
        var conflicting = await events.EnqueueAsync<ShipmentReceiptRecorder>(delivery with { Event = otherEvent.RootElement.Clone() }, OutboxRelay.KeyOf(stuck));
        events.Advance(TimeSpan.FromSeconds(1));
        await events.DispatchManyAsync(OutboxRelay.BatchSize);
        events.OutboxReads.FailEveryRead();
        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);
        events.OutboxReads.FailFromRead(2);
        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);
        events.OutboxReads.Stop();
        await events.QueryJobsAsync((context, token) => context.Runs.Where(r => r.Id == conflicting).ExecuteUpdateAsync(s => s.SetProperty(r => r.Payload, JobPayloads.Serialize(delivery)), token));

        await relay.EnqueueDueAsync(TestContext.Current.CancellationToken);

        Assert.NotNull((await events.OutboxRowAsync(stuck))!.RelayedAt);
        Assert.Contains(conflicting, (await events.DeliveriesAsync(stuck)).Select(run => run.Id));
    }

    [Fact]
    public async Task EnqueueDueAsync_MoreEventsThanAPage_PassesThemAllOn()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var ids = await events.DispatchManyAsync(OutboxRelay.BatchSize + 1);

        await RelayOf(events).EnqueueDueAsync(TestContext.Current.CancellationToken);

        var rows = await events.OutboxRowsAsync(ids);
        Assert.Equal(ids.Count, rows.Count);
        Assert.All(rows, row => Assert.NotNull(row.RelayedAt));
        var keys = ids.Select(OutboxRelay.KeyOf).ToList();
        Assert.Equal(2 * ids.Count, await events.QueryJobsAsync((context, token) => context.Runs.CountAsync(r => keys.Contains(r.IdempotencyKey), token)));
    }

    private static OutboxRelay RelayOf(IntegrationEventsDatabase events) => events.First.Services.GetRequiredService<OutboxRelay>();
}
