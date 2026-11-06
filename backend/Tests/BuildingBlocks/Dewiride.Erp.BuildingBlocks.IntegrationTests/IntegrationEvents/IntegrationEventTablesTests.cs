using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents;

// The configurations reach a module's model only through their attributes, applied when the context declares the DbSet; a model
// without them would still match its own snapshot, so the mapping is read from the model itself.
[Collection(IntegrationEventPublishers.Name)]
public sealed class IntegrationEventTablesTests
{
    private static readonly string[] OutboxFilters = ["[RelayedAt] IS NOT NULL", "[RelayedAt] IS NULL"];

    private static readonly string[] ProcessedKey = [nameof(ProcessedMessage.EventId), nameof(ProcessedMessage.Subscriber)];

    [Fact]
    public async Task Model_ContextDeclaringBothTables_MapsThemAsTheirConfigurationsSay()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        await using var scope = events.First.Services.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<SampleDbContext>().Model;

        var outbox = model.FindEntityType(typeof(OutboxMessage))!;
        Assert.Equal(("OutboxMessages", SampleDbContext.SchemaName), (outbox.GetTableName(), outbox.GetSchema()));
        Assert.Equal(("varchar(128)", "json", "varchar(64)"), (outbox.FindProperty(nameof(OutboxMessage.EventName))!.GetColumnType(), outbox.FindProperty(nameof(OutboxMessage.Payload))!.GetColumnType(), outbox.FindProperty(nameof(OutboxMessage.CorrelationId))!.GetColumnType()));
        Assert.IsType<StronglyTypedIdConverter<IntegrationEventId>>(outbox.FindPrimaryKey()!.Properties.Single().GetValueConverter());
        Assert.Equal(OutboxFilters, outbox.GetIndexes().Select(index => index.GetFilter()).Order(StringComparer.Ordinal));

        var processed = model.FindEntityType(typeof(ProcessedMessage))!;
        Assert.Equal(("ProcessedMessages", SampleDbContext.SchemaName), (processed.GetTableName(), processed.GetSchema()));
        Assert.Equal(ProcessedKey, processed.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal("varchar(128)", processed.FindProperty(nameof(ProcessedMessage.Subscriber))!.GetColumnType());
    }

    [Fact]
    public async Task Tables_Catalogue_NamesTheContextsThatDeclareThem()
    {
        await using var events = await IntegrationEventsDatabase.CreateAsync();
        var tables = events.First.Services.GetRequiredService<IntegrationEventTables>();

        Assert.Equal(typeof(SampleDbContext), Assert.Single(tables.Outboxes));
        Assert.Equal(typeof(SampleDbContext), Assert.Single(tables.ProcessedRecords));
    }
}
