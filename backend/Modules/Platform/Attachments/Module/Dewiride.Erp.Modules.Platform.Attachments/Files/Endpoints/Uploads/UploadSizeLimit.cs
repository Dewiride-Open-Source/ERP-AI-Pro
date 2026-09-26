using Dewiride.Erp.BuildingBlocks.Attachments;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Uploads;

// Routing applies this limit to the matched request before any middleware reads the body, replacing the global
// Erp:Platform:Host:MaxRequestBodyBytes for uploads only; it follows the configured MaxSizeBytes as it refreshes.
internal sealed class UploadSizeLimit(IOptionsMonitor<AttachmentsOptions> options) : IRequestSizeLimitMetadata
{
    // Room for the multipart boundaries and part headers around the file itself.
    public const long MultipartAllowanceBytes = 64 * 1024;

    public long? MaxRequestBodySize => options.CurrentValue.MaxSizeBytes + MultipartAllowanceBytes;
}
