namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Responses;

/// <summary>A link that downloads one attachment for the person who asked for it.</summary>
/// <param name="Url">Path of the download on this origin, including its token; usable by the same person until ExpiresAt.</param>
/// <param name="ExpiresAt">UTC time after which the link answers not found.</param>
internal sealed record DownloadLinkResponse(string Url, DateTimeOffset ExpiresAt);
