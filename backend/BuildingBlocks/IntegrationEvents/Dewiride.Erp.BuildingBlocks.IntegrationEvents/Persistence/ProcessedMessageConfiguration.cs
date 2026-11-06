using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public const string TableName = "ProcessedMessages";

    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(m => new { m.EventId, m.Subscriber });
        builder.Property(m => m.Subscriber).HasMaxLength(JobDefinition.NameMaxLength).IsUnicode(false);
        builder.HasIndex(m => m.ProcessedAt);
    }
}
