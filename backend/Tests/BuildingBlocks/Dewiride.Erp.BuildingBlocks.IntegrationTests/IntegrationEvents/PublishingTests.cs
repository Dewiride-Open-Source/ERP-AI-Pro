using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Publishing;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Shipments;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents;

[Collection(IntegrationEventPublishers.Name)]
public sealed class PublishingTests
{
    [Fact]
    public async Task Publish_UnitCommits_WritesOneOutboxRowWithTheChange()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var origin = IntegrationEventsDatabase.OriginOf(events.First);

        var (shipment, id) = await events.DispatchAsync(parcels: 4);

        var row = await events.OutboxRowAsync(id);
        Assert.NotNull(row);
        Assert.Equal((ShipmentDispatched.Name, events.FirstClock.GetUtcNow(), origin.CorrelationId, (DateTimeOffset?)null), (row.EventName, row.OccurredAt, row.CorrelationId, row.RelayedAt));
        using var payload = JsonDocument.Parse(row.Payload);
        Assert.Equal(shipment.Value, payload.RootElement.GetProperty("shipmentId").GetGuid());
        Assert.Equal(4, payload.RootElement.GetProperty("parcels").GetInt32());
        Assert.True(await events.QueryAsync((context, token) => context.Shipments.AnyAsync(s => s.Id == shipment, token)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publish_UnitFailsOrThrowsAfterPublishing_LeavesNoRow(bool throws)
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var shipment = SampleShipmentId.Create();
        var published = default(IntegrationEventId);

        var unit = events.RunUnitAsync(async (context, services, token) =>
        {
            published = services.GetRequiredService<IIntegrationEventPublisher>().Publish(context, new ShipmentDispatched(shipment.Value, 1, events.FirstClock.GetUtcNow()));
            await context.SaveChangesAsync(token);

            return throws ? throw new InvalidOperationException("The unit gave up after publishing.") : Error.Failure("test.refused", "The unit refused after publishing.");
        });
        if (throws)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => unit);
        }
        else
        {
            Assert.True((await unit).IsFailure);
        }

        Assert.Null(await events.OutboxRowAsync(published));
    }

    [Fact]
    public async Task Publish_OutsideAUnitOfWork_IsRefused()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        await using var scope = events.First.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

        var refusal = Assert.Throws<InvalidOperationException>(() => publisher.Publish(context, new ShipmentDispatched(Guid.CreateVersion7(), 1, events.FirstClock.GetUtcNow())));

        Assert.Contains("outside a unit of work", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Publish_ContextWithoutAnOutbox_IsRefused()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        await using var scope = events.First.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        await using var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var refusal = Assert.Throws<InvalidOperationException>(() => publisher.Publish(context, new ShipmentDispatched(Guid.CreateVersion7(), 1, events.FirstClock.GetUtcNow())));

        Assert.Contains("has no outbox", refusal.Message, StringComparison.Ordinal);
    }
}
