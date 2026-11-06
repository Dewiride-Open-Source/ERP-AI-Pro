using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public const string TableName = "OutboxMessages";

    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.EventName).HasMaxLength(IntegrationEventNames.MaxLength).IsUnicode(false);
        builder.Property(m => m.Payload).HasColumnType("json");
        builder.Property(m => m.CorrelationId).HasMaxLength(OutboxMessage.CorrelationIdMaxLength).IsUnicode(false);
        builder.HasIndex(m => new { m.OccurredAt, m.Id }).HasFilter("[RelayedAt] IS NULL");
        builder.HasIndex(m => m.RelayedAt).HasFilter("[RelayedAt] IS NOT NULL");
    }
}
