using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Persistence;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.FileName).HasMaxLength(Attachment.FileNameMaxLength);
        builder.Property(a => a.ContentType).HasMaxLength(Attachment.ContentTypeMaxLength).IsUnicode(false);
        builder.Property(a => a.ScanStatus).HasConversion<byte>();
        builder.HasOne(a => a.Content).WithMany().HasForeignKey(a => a.ContentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.CreatedAt);
    }
}
