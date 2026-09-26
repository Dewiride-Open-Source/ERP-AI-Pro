using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Persistence;

internal sealed class StoredContentConfiguration : IEntityTypeConfiguration<StoredContent>
{
    public void Configure(EntityTypeBuilder<StoredContent> builder)
    {
        builder.ToTable("StoredContents");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Sha256).HasMaxLength(StoredContent.Sha256Length).IsFixedLength();
        builder.Property(c => c.KeyId).HasMaxLength(StoredContent.KeyIdMaxLength).IsUnicode(false);

        // Not unique: two identical uploads racing each other both keep their own copy, which is harmless, rather than one
        // of them failing.
        builder.HasIndex(c => new { c.Sha256, c.Length });
        builder.HasIndex(c => c.KeyId);
    }
}
