namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Uploads;

internal sealed record MultipartFile(string? FileName, string? ContentType, Stream Content);
