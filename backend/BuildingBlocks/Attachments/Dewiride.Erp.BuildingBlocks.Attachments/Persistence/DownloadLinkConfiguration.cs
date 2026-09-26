using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Persistence;

internal sealed class DownloadLinkConfiguration : IEntityTypeConfiguration<DownloadLink>
{
    public void Configure(EntityTypeBuilder<DownloadLink> builder)
    {
        builder.ToTable("DownloadLinks");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.TokenHash).HasMaxLength(DownloadLink.TokenHashLength).IsFixedLength();
        builder.HasIndex(l => l.TokenHash).IsUnique();
        builder.HasOne<Attachment>().WithMany().HasForeignKey(l => l.AttachmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
