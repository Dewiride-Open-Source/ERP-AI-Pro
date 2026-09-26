namespace Dewiride.Erp.BuildingBlocks.Attachments.Service;

public sealed record AttachmentUpload(string? FileName, string? ContentType, Stream Content);
