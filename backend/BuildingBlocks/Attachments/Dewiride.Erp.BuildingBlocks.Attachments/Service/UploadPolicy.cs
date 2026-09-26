namespace Dewiride.Erp.BuildingBlocks.Attachments.Service;

public sealed record UploadPolicy(long MaxSizeBytes, IReadOnlyList<string> AllowedContentTypes);
