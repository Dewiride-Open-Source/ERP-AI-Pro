using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Attachments.Inspection;

namespace Dewiride.Erp.BuildingBlocks.Attachments;

public sealed class AttachmentsOptions
{
    public const string SectionName = "Erp:Platform:Attachments";

    public const long MaxSizeBytesCeiling = 100L * 1024 * 1024;

    public const string DefaultContainerName = "attachments";

    public string? BlobServiceUri { get; set; }

    public string? EmulatorHost { get; set; }

    [Required]
    public string ContainerName { get; set; } = DefaultContainerName;

    [Range(1L, MaxSizeBytesCeiling)]
    public long MaxSizeBytes { get; set; } = 25L * 1024 * 1024;

    [Required]
    public string AllowedContentTypes { get; set; } = string.Join(ContentTypes.ListSeparator, ContentTypes.Known);

    [Range(typeof(TimeSpan), "00:00:30", "01:00:00")]
    public TimeSpan DownloadLinkLifetime { get; set; } = TimeSpan.FromMinutes(5);

    // At most ten minutes: the web app's rewrite proxy gives up on an /api call after 660 seconds.
    [Range(typeof(TimeSpan), "00:00:30", "00:10:00")]
    public TimeSpan TransferTimeout { get; set; } = TimeSpan.FromMinutes(10);

    // Below seven days: Blob Storage discards uncommitted blocks a week after the last write, and the sweeper must see a
    // reservation before that happens.
    [Range(typeof(TimeSpan), "00:01:00", "6.00:00:00")]
    public TimeSpan UploadReservationLifetime { get; set; } = TimeSpan.FromHours(1);

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromMinutes(15);

    // Each upload in flight holds a staging block in memory for its whole transfer, however slowly its body arrives.
    [Range(1, 1024)]
    public int MaxConcurrentUploads { get; set; } = 16;

    public string? EncryptionKey { get; set; }

    public string? RetiredEncryptionKeys { get; set; }
}
