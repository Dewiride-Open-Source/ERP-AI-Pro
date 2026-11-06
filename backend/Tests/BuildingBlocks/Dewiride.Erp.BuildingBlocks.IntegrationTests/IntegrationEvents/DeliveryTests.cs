using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Publishing;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Integration.Consumers;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Shipments;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Dewiride.Erp.BuildingBlocks.Jobs.Running.Attempts;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents;

[Collection(IntegrationEventPublishers.Name)]
public sealed class DeliveryTests
{
    [Fact]
    public async Task RunAsync_Delivery_HandlesTheEventAsTheSystemTogetherWithItsProcessedRecord()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (shipment, id) = await events.DispatchAsync(parcels: 2);
        await events.RelayAsync(id);

        var outcome = await events.RunAsync((await events.DeliveryAsync(id, events.Recorder)).Id);

        Assert.Equal(JobRunStatus.Succeeded, outcome.Status);
        var receipt = Assert.Single(await events.ReceiptsAsync(shipment));
        Assert.Equal((nameof(ShipmentReceiptRecorder), 2, id.Value, ActorIds.System), (receipt.RecordedBy, receipt.Parcels, receipt.EventId, receipt.CreatedBy));
        Assert.Equal(events.Recorder, Assert.Single(await events.ProcessedAsync(id)).Subscriber);
    }

    [Fact]
    public async Task RunAsync_DeliveryPublishedByAnotherModule_RecordsTheChangeInTheSubscribersOwnContext()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (shipment, id) = await events.DispatchAsync();
        var delivery = (await events.DeliveryPayloadAsync(id)) with { Source = JobsDbContext.SchemaName };

        var outcome = await events.RunAsync(await events.EnqueueAsync<ShipmentReceiptRecorder>(delivery, JobsDatabase.NewKey()));

        Assert.Equal(JobRunStatus.Succeeded, outcome.Status);
        Assert.Equal(nameof(ShipmentReceiptRecorder), Assert.Single(await events.ReceiptsAsync(shipment)).RecordedBy);
        Assert.Equal(events.Recorder, Assert.Single(await events.ProcessedAsync(id)).Subscriber);
    }

    [Fact]
    public async Task RunAsync_DeliveryOfAnEventItsSubscriberHandled_ChangesNothing()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (shipment, id) = await events.DispatchAsync();
        await events.RelayAsync(id);
        await events.RunAsync((await events.DeliveryAsync(id, events.Recorder)).Id);
        var again = await events.EnqueueAsync<ShipmentReceiptRecorder>(await events.DeliveryPayloadAsync(id), JobsDatabase.NewKey());

        var outcome = await events.RunAsync(again);

        Assert.Equal(JobRunStatus.Succeeded, outcome.Status);
        Assert.Single(await events.ReceiptsAsync(shipment));
        Assert.Single(await events.ProcessedAsync(id));
        Assert.Equal(1, events.Script.CallsOf<ShipmentReceiptRecorder>(shipment.Value));
        Assert.Contains(IntegrationEventsDatabase.LogsOf(events.First).GetSnapshot(), record => record.Message.StartsWith("The event was already handled", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_TwoRunsOfOneDeliveryAtOnce_ApplyTheChangeOnce()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (shipment, id) = await events.DispatchAsync();
        await events.RelayAsync(id);
        var first = (await events.DeliveryAsync(id, events.Recorder)).Id;
        var second = await events.EnqueueAsync<ShipmentReceiptRecorder>(await events.DeliveryPayloadAsync(id), JobsDatabase.NewKey());
        var arrived = 0;
        var bothArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        events.Script.Answer<ShipmentReceiptRecorder>(async (_, _, _, _) =>
        {
            if (Interlocked.Increment(ref arrived) == 2)
            {
                bothArrived.TrySetResult();
            }

            await release.Task;

            return Result.Success();
        });

        var runs = Task.WhenAll(IntegrationEventsDatabase.RunAsync(events.First, first), IntegrationEventsDatabase.RunAsync(events.Second, second));
        await bothArrived.Task.WaitAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        release.SetResult();
        var outcomes = await runs;

        Assert.Single(outcomes, outcome => outcome.Status == JobRunStatus.Succeeded);
        var conflicted = Assert.Single(outcomes, outcome => outcome.Status == JobRunStatus.Failed);
        Assert.Equal(EfUnitOfWork.ConcurrencyConflictCode, conflicted.ErrorCode);
        events.Advance(JobRetryPolicy.Default.FirstDelay);
        var retried = outcomes[0].Status == JobRunStatus.Failed ? first : second;
        Assert.Equal(JobRunStatus.Succeeded, (await events.RunAsync(retried)).Status);
        Assert.Single(await events.ReceiptsAsync(shipment));
        Assert.Single(await events.ProcessedAsync(id));
    }

    [Fact]
    public async Task RunAsync_FailingSubscriber_IsRetriedThenDeadLetteredWithoutHoldingBackTheOther()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync(new JobRetryPolicy(2, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)));
        events.Script.Answer<ShipmentLabelPrinter>((_, _, _, _) => Task.FromResult(Result.Fail(Error.Unavailable("test.printer-offline", "The label printer is offline."))));
        var (shipment, id) = await events.DispatchAsync();
        await events.RelayAsync(id);
        var printer = (await events.DeliveryAsync(id, events.Printer)).Id;

        var printedFirst = await events.RunAsync(printer);
        var recorded = await events.RunAsync((await events.DeliveryAsync(id, events.Recorder)).Id);
        events.Advance(TimeSpan.FromMinutes(1));
        var printedLast = await events.RunAsync(printer);

        Assert.Equal((JobRunStatus.Failed, "test.printer-offline"), (printedFirst.Status, printedFirst.ErrorCode));
        Assert.Equal(JobRunStatus.Succeeded, recorded.Status);
        Assert.Equal((JobRunStatus.DeadLettered, "test.printer-offline"), (printedLast.Status, printedLast.ErrorCode));
        Assert.Equal(nameof(ShipmentReceiptRecorder), Assert.Single(await events.ReceiptsAsync(shipment)).RecordedBy);
        Assert.Equal(events.Recorder, Assert.Single(await events.ProcessedAsync(id)).Subscriber);
    }

    [Fact]
    public async Task RunAsync_DeliveryWhoseEventNoLongerReads_IsDeadLetteredAsUnreadable()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (shipment, id) = await events.DispatchAsync();
        using var missingParcels = JsonDocument.Parse($$"""{"shipmentId":"{{shipment.Value}}","dispatchedAt":"2026-11-01T06:30:00+00:00"}""");
        var delivery = (await events.DeliveryPayloadAsync(id)) with { Event = missingParcels.RootElement.Clone() };

        var outcome = await events.RunAsync(await events.EnqueueAsync<ShipmentReceiptRecorder>(delivery, JobsDatabase.NewKey()));

        Assert.Equal((JobRunStatus.DeadLettered, "integration-event.unreadable"), (outcome.Status, outcome.ErrorCode));
        Assert.Empty(await events.ReceiptsAsync(shipment));
        Assert.Equal(0, events.Script.CallsOf<ShipmentReceiptRecorder>(shipment.Value));
    }

    [Fact]
    public async Task RunAsync_DeliveryOfAnotherEventThanTheSubscribers_IsDeadLetteredAsUnreadable()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (shipment, id) = await events.DispatchAsync();
        var delivery = (await events.DeliveryPayloadAsync(id)) with { EventName = "test.events.shipment-lost" };

        var outcome = await events.RunAsync(await events.EnqueueAsync<ShipmentReceiptRecorder>(delivery, JobsDatabase.NewKey()));

        Assert.Equal((JobRunStatus.DeadLettered, "integration-event.unreadable"), (outcome.Status, outcome.ErrorCode));
        Assert.Empty(await events.ReceiptsAsync(shipment));
        Assert.Equal(0, events.Script.CallsOf<ShipmentReceiptRecorder>(shipment.Value));
    }

    [Fact]
    public async Task RunAsync_EventAsOldAsTheProcessedRecordsAreKept_IsDeadLetteredAsTooOldWhileAYoungerOneIsHandled()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (shipment, id) = await events.DispatchAsync();
        var oldest = events.FirstClock.GetUtcNow() - events.RunRetention;
        var delivery = await events.DeliveryPayloadAsync(id);

        var tooOld = await events.RunAsync(await events.EnqueueAsync<ShipmentReceiptRecorder>(delivery with { OccurredAt = oldest }, JobsDatabase.NewKey()));
        var younger = await events.RunAsync(await events.EnqueueAsync<ShipmentReceiptRecorder>(delivery with { OccurredAt = oldest + TimeSpan.FromTicks(1) }, JobsDatabase.NewKey()));

        Assert.Equal((JobRunStatus.DeadLettered, "integration-event.too-old"), (tooOld.Status, tooOld.ErrorCode));
        Assert.Equal(JobRunStatus.Succeeded, younger.Status);
        Assert.Equal(1, events.Script.CallsOf<ShipmentReceiptRecorder>(shipment.Value));
    }

    [Fact]
    public async Task RunAsync_SubscriberPublishes_ItsEventFollowsOneDeliveryMoreThanTheDeliveredEvent()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (_, id) = await events.DispatchAsync();
        var followUp = SampleShipmentId.Create();
        events.Script.Answer<ShipmentReceiptRecorder>((_, _, database, _) => DispatchAsync(database, followUp, events));
        var delivery = (await events.DeliveryPayloadAsync(id)) with { Depth = IntegrationEventChain.MaxDepth - 1 };

        var outcome = await events.RunAsync(await events.EnqueueAsync<ShipmentReceiptRecorder>(delivery, JobsDatabase.NewKey()));

        Assert.Equal(JobRunStatus.Succeeded, outcome.Status);
        Assert.Equal(0, (await events.OutboxRowAsync(id))!.Depth);
        Assert.Equal(IntegrationEventChain.MaxDepth, (await events.OutboxRowAsync(events.Published.Of(followUp)))!.Depth);
    }

    [Fact]
    public async Task RunAsync_SubscriberPublishesPastTheLongestChain_FailsAndSavesNothing()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var (shipment, id) = await events.DispatchAsync();
        var followUp = SampleShipmentId.Create();
        events.Script.Answer<ShipmentReceiptRecorder>((_, _, database, _) => DispatchAsync(database, followUp, events));
        var delivery = (await events.DeliveryPayloadAsync(id)) with { Depth = IntegrationEventChain.MaxDepth };

        var outcome = await events.RunAsync(await events.EnqueueAsync<ShipmentReceiptRecorder>(delivery, JobsDatabase.NewKey()));

        Assert.Equal((JobRunStatus.Failed, JobAttemptErrors.ExceptionCode), (outcome.Status, outcome.ErrorCode));
        Assert.Contains(IntegrationEventsDatabase.LogsOf(events.First).GetSnapshot(), record => record.Exception is InvalidOperationException { Message: var message } && message.Contains($"a chain of {IntegrationEventChain.MaxDepth + 1} deliveries", StringComparison.Ordinal));
        Assert.Empty(events.Published.AllOf(followUp));
        Assert.False(await events.QueryAsync((context, token) => context.Shipments.AnyAsync(s => s.Id == followUp, token)));
        Assert.Empty(await events.ReceiptsAsync(shipment));
        Assert.Empty(await events.ProcessedAsync(id));
    }

    private static Task<Result> DispatchAsync(SampleDbContext database, SampleShipmentId shipment, IntegrationEventsDatabase events)
    {
        database.Shipments.Add(SampleShipment.Dispatch(shipment, 1, events.FirstClock.GetUtcNow()));

        return Task.FromResult(Result.Success());
    }
}
