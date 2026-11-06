using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Integration.Consumers;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Shipments;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Running;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents;

// The one test that lets a runner poll, so it also passes on and delivers whatever events the collection's earlier tests left
// waiting, and waits for its own event alone. The clocks never move: the commit's announcement wakes the runner, and each enqueue
// wakes it again.
[Collection(IntegrationEventPublishers.Name)]
public sealed class RunnerPollTests
{
    private static readonly string[] Subscribers = [nameof(ShipmentLabelPrinter), nameof(ShipmentReceiptRecorder)];

    [Fact]
    public async Task ExecuteAsync_EventCommitted_IsPassedOnAndDeliveredToEverySubscriberWithoutWaitingForAPollInterval()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var shipment = SampleShipmentId.Create();
        var recorded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var printed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        events.Script.Answer<ShipmentReceiptRecorder>((integrationEvent, _, _) => Arrive(integrationEvent.ShipmentId, shipment, recorded));
        events.Script.Answer<ShipmentLabelPrinter>((integrationEvent, _, _) => Arrive(integrationEvent.ShipmentId, shipment, printed));
        var service = events.First.Services.GetServices<IHostedService>().OfType<JobRunnerService>().Single();
        await service.StartAsync(TestContext.Current.CancellationToken);
        IntegrationEventsDatabase.LifetimeOf(events.First).NotifyStarted();

        var (_, id) = await events.DispatchAsync(shipment);
        await Task.WhenAll(recorded.Task, printed.Task).WaitAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        await service.StopAsync(CancellationToken.None);

        Assert.All(await events.DeliveriesAsync(id), run => Assert.Equal(JobRunStatus.Succeeded, run.Status));
        Assert.Equal(Subscribers, (await events.ReceiptsAsync(shipment)).Select(receipt => receipt.RecordedBy));
        Assert.Equal(2, (await events.ProcessedAsync(id)).Count);
    }

    private static Task<Result> Arrive(Guid delivered, SampleShipmentId awaited, TaskCompletionSource arrived)
    {
        if (delivered == awaited.Value)
        {
            arrived.TrySetResult();
        }

        return Task.FromResult(Result.Success());
    }
}
