using Dewiride.Erp.BuildingBlocks.Attachments.Domain;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Responses;

/// <summary>A stored attachment.</summary>
/// <param name="Id">Identifier of the attachment.</param>
/// <param name="FileName">File name the uploader gave, without any directory.</param>
/// <param name="ContentType">Media type the upload declared and its leading bytes confirmed.</param>
/// <param name="SizeBytes">Size of the original file in bytes.</param>
/// <param name="Sha256">SHA-256 of the original file, as 64 lowercase hexadecimal digits.</param>
/// <param name="ScanStatus">Outcome of the virus-scan hook when the file was uploaded.</param>
/// <param name="CreatedAt">UTC time the attachment was stored.</param>
/// <param name="CreatedBy">Actor who uploaded the file.</param>
internal sealed record AttachmentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    AttachmentScanStatus ScanStatus,
    DateTimeOffset CreatedAt,
    Guid CreatedBy);
