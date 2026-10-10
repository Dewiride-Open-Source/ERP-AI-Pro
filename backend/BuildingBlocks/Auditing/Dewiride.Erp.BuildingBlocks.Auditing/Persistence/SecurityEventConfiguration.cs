using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Persistence;

internal sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.ToTable("SecurityEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Kind).HasConversion<byte>();
        builder.Property(e => e.Detail).HasMaxLength(SecurityEvent.DetailMaxLength).IsUnicode(false);
        builder.Property(e => e.ClientAddress).HasMaxLength(SecurityEvent.ClientAddressMaxLength).IsUnicode(false);
        builder.Property(e => e.CorrelationId).HasMaxLength(SecurityEvent.CorrelationIdMaxLength).IsUnicode(false);
        builder.HasIndex(e => e.OccurredAt);
        builder.HasIndex(e => new { e.ActorObjectId, e.OccurredAt });
    }
}
