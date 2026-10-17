using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

// Every context maps the change records, so the records of a change are inserted by the same SaveChangesAsync as the change and
// commit with it whether or not a transaction was begun. Only the context that owns the audit schema creates the table; every
// other context maps it excluded from its migrations. The table has no generated, default or computed column, so SQL Server
// receives a plain INSERT, which the append-only role allows.
public static class ChangeRecordMapping
{
    public const string Schema = "audit";

    public const string Table = "ChangeRecords";

    public const int KeyMaxLength = 64;

    public const int ClientAddressMaxLength = 64;

    public const int CorrelationIdMaxLength = 64;

    public static ModelBuilder MapChangeRecords(this ModelBuilder modelBuilder, bool ownsTable)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<ChangeRecord>(builder => Configure(builder, ownsTable));

        return modelBuilder;
    }

    private static void Configure(EntityTypeBuilder<ChangeRecord> builder, bool ownsTable)
    {
        if (ownsTable)
        {
            builder.ToTable(Table, Schema);
        }
        else
        {
            builder.ToTable(Table, Schema, table => table.ExcludeFromMigrations());
        }

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion<StronglyTypedIdConverter<ChangeRecordId>>().ValueGeneratedNever();
        builder.Property(r => r.Operation).HasConversion<byte>();
        builder.Property(r => r.EntityType).HasMaxLength(KeyMaxLength).IsUnicode(false);
        builder.Property(r => r.SubjectType).HasMaxLength(KeyMaxLength).IsUnicode(false);
        builder.Property(r => r.ClientAddress).HasMaxLength(ClientAddressMaxLength).IsUnicode(false);
        builder.Property(r => r.CorrelationId).HasMaxLength(CorrelationIdMaxLength).IsUnicode(false);
        builder.Property(r => r.Changes).HasColumnType("json");
        builder.HasIndex(r => r.OccurredAt);
        builder.HasIndex(r => new { r.EntityType, r.OccurredAt });
        builder.HasIndex(r => new { r.EntityId, r.OccurredAt });
        builder.HasIndex(r => new { r.SubjectId, r.OccurredAt });
        builder.HasIndex(r => new { r.ActorId, r.OccurredAt });
        builder.HasIndex(r => r.CorrelationId).HasFilter("[CorrelationId] IS NOT NULL");
    }
}
