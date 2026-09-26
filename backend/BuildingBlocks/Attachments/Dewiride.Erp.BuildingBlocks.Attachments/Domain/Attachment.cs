using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Domain;

internal sealed class Attachment : AggregateRoot<AttachmentId>, IAuditable, ISoftDeletable
{
    public const int FileNameMaxLength = 255;

    public const int ContentTypeMaxLength = 127;

    private Attachment(AttachmentId id)
        : base(id)
    {
    }

    private Attachment(AttachmentId id, StoredContent content, string fileName, string contentType, AttachmentScanStatus scanStatus)
        : base(id)
    {
        ContentId = content.Id;
        Content = content;
        FileName = fileName;
        ContentType = contentType;
        ScanStatus = scanStatus;
    }

    public StoredContentId ContentId { get; private set; }

    public StoredContent Content { get; private set; } = null!;

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public AttachmentScanStatus ScanStatus { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public static Attachment Create(StoredContent content, string fileName, string contentType, AttachmentScanStatus scanStatus)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fileName.Length, FileNameMaxLength, nameof(fileName));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(contentType.Length, ContentTypeMaxLength, nameof(contentType));

        return new Attachment(AttachmentId.Create(), content, fileName, contentType, scanStatus);
    }
}
