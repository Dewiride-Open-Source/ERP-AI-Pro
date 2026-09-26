using Dewiride.Erp.BuildingBlocks.Attachments.Domain;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Service;

public sealed record AttachmentDetails(
    AttachmentId Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    AttachmentScanStatus ScanStatus,
    DateTimeOffset CreatedAt,
    Guid CreatedBy);
