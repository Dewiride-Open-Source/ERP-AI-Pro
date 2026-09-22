using Dewiride.Erp.BuildingBlocks.Idempotency.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Idempotency.Persistence;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyKeys");
        builder.HasKey(r => new { r.ActorId, r.Key });
        builder.Property(r => r.Fingerprint).HasMaxLength(IdempotencyRecord.FingerprintLength).IsFixedLength();
        builder.Property(r => r.Status).HasConversion<byte>();
        builder.Property(r => r.ContentType).HasMaxLength(IdempotencyRecord.ContentTypeMaxLength);
        builder.Property(r => r.Location).HasMaxLength(IdempotencyRecord.LocationMaxLength);
        builder.HasIndex(r => r.ExpiresAt);
    }
}
