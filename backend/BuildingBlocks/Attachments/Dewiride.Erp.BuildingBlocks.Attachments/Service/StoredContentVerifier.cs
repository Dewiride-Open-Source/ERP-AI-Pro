using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Service;

// A StoredContent row proves nothing about its blob: storage restored from another point in time can lack the blob, and a
// key id can come back carrying different material. Reading the header and opening its data key proves both, at the cost of
// one ranged read of at most EnvelopeHeader.MaxLength bytes.
internal sealed partial class StoredContentVerifier(IDocumentStore store, KeyRing keys, ILogger<StoredContentVerifier> logger)
{
    public async Task<bool> CanOpenAsync(StoredContentId contentId, CancellationToken cancellationToken)
    {
        try
        {
            await using var head = await store.OpenHeadAsync(contentId, EnvelopeHeader.MaxLength, cancellationToken).ConfigureAwait(false);
            var header = await EnvelopeHeader.ReadAsync(head, cancellationToken).ConfigureAwait(false);
            CryptographicOperations.ZeroMemory(header.OpenDataKey(keys, contentId));

            return true;
        }
        catch (StoredContentMissingException exception)
        {
            LogMissing(logger, exception, contentId.Value);
            return false;
        }
        catch (EnvelopeFormatException exception)
        {
            LogUnopenable(logger, exception, contentId.Value);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Stored content {ContentId} has no blob")]
    private static partial void LogMissing(ILogger logger, Exception exception, Guid contentId);

    [LoggerMessage(Level = LogLevel.Error, Message = "The blob of stored content {ContentId} cannot be opened with the configured attachment keys")]
    private static partial void LogUnopenable(ILogger logger, Exception exception, Guid contentId);
}
