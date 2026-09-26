using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Uploads;

// Reads the request body as it arrives instead of binding IFormFile, which would buffer the whole file on disk or in memory
// before the upload checks run. The first part that carries a file name is the upload; parts before it are skipped and
// anything after it is never read.
internal static class MultipartFileReader
{
    private const string FormData = "multipart/form-data";

    // RFC 2046 section 5.1.1 caps a boundary at 70 characters.
    private const int MaxBoundaryLength = 70;

    public static async Task<Result<MultipartFile>> ReadFirstFileAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType) || !mediaType.MediaType.Equals(FormData, StringComparison.OrdinalIgnoreCase))
        {
            return AttachmentErrors.MultipartRequired;
        }

        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > MaxBoundaryLength)
        {
            return AttachmentErrors.MultipartRequired;
        }

        var reader = new MultipartReader(boundary, request.Body);
        while (await reader.ReadNextSectionAsync(cancellationToken).ConfigureAwait(false) is { } section)
        {
            if (ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition) && disposition.IsFileDisposition())
            {
                var fileName = disposition.FileNameStar.HasValue ? disposition.FileNameStar : disposition.FileName;

                return new MultipartFile(HeaderUtilities.RemoveQuotes(fileName).Value, section.ContentType, section.Body);
            }
        }

        return AttachmentErrors.FileMissing;
    }
}
