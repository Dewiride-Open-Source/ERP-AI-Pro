using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.ArchitectureTests.Rules.Probes.Persistence;

// Contexts built as a module's is, each mapping what one rule on a module's model accepts or refuses, for the rules that would
// otherwise only meet the product's contexts, none of which maps the integration-event tables yet.
public static class ProbeContexts
{
    public sealed class Recording() : ModuleDbContext(Options<Recording>(), SchemaName)
    {
        public const string SchemaName = "probe_ledgers";

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    }

    public sealed class OutboxOnly() : ModuleDbContext(Options<OutboxOnly>(), "probe_outboxes")
    {
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    }

    public sealed class Remapped() : ModuleDbContext(Options<Remapped>(), SchemaName)
    {
        public const string SchemaName = "probe_remapped";

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<OutboxMessage>().ToTable("Outbox").Property(m => m.Payload).HasColumnType("nvarchar(max)");
            modelBuilder.Entity<ProcessedMessage>().ToTable("Processed");
        }
    }

    public sealed class Rekeyed() : ModuleDbContext(Options<Rekeyed>(), SchemaName)
    {
        public const string SchemaName = "probe_rekeyed";

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<OutboxMessage>().HasIndex(m => m.RelayedAt).HasFilter(null);
            modelBuilder.Entity<ProcessedMessage>().HasKey(m => new { m.Subscriber, m.EventId });
        }
    }

    public sealed class Foreign() : ModuleDbContext(Options<Foreign>(), "probe_foreign")
    {
        public const string BagName = "ProbeBag";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.SharedTypeEntity<Dictionary<string, object>>(BagName, bag =>
            {
                bag.Property<Guid>("Id");
                bag.HasKey("Id");
            });
        }
    }

    private static DbContextOptions<TContext> Options<TContext>()
        where TContext : DbContext => new DbContextOptionsBuilder<TContext>().UseSqlServer().Options;
}
