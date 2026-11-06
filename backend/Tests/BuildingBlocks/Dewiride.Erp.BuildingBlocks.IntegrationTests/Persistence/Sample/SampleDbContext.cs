using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Receipts;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Shipments;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample.Notes;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

// The one context of this assembly, because a module context applies every entity configuration of its assembly and a command
// commits the one context its assembly owns. Its shipments publish integration events and its receipts are what its subscribers
// record, the way a module that answers its own events would.
public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "test_sample";

    public DbSet<SampleAggregate> Samples => Set<SampleAggregate>();

    public DbSet<SampleNote> Notes => Set<SampleNote>();

    public DbSet<SampleShipment> Shipments => Set<SampleShipment>();

    public DbSet<SampleReceipt> Receipts => Set<SampleReceipt>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
}
