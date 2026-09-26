using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Persistence;

internal sealed class AttachmentsDbContext(DbContextOptions<AttachmentsDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "files";

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<StoredContent> StoredContents => Set<StoredContent>();

    public DbSet<UploadReservation> UploadReservations => Set<UploadReservation>();

    public DbSet<DownloadLink> DownloadLinks => Set<DownloadLink>();

    public DbSet<DownloadRedemption> DownloadRedemptions => Set<DownloadRedemption>();
}
