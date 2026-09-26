namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Responses;

/// <summary>What the API accepts as an upload, so a client can refuse a file before sending it.</summary>
/// <param name="MaxSizeBytes">Largest file, in bytes, the API stores.</param>
/// <param name="AllowedContentTypes">Media types the API stores; any other is refused.</param>
internal sealed record UploadPolicyResponse(long MaxSizeBytes, IReadOnlyList<string> AllowedContentTypes);
