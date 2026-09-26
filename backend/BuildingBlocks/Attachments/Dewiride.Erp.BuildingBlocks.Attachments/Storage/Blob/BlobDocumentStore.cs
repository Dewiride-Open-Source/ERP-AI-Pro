using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob;

// Blob names are random content ids, never content hashes: names appear in storage logs and request traces, and a hash
// would let anyone who sees them test whether a known document is stored.
internal sealed class BlobDocumentStore(AttachmentBlobClients clients) : IDocumentStore
{
    public IDocumentUpload BeginUpload(StoredContentId contentId) =>
        new BlobDocumentUpload(new BlockStagingStream(clients.Container.GetBlockBlobClient(BlobName(contentId))));

    public async Task<Stream> OpenReadAsync(StoredContentId contentId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await clients.Container.GetBlobClient(BlobName(contentId)).DownloadStreamingAsync(new BlobDownloadOptions(), cancellationToken).ConfigureAwait(false);

            return response.Value.Content;
        }
        catch (RequestFailedException exception) when (exception.ErrorCode == BlobErrorCode.BlobNotFound || exception.ErrorCode == BlobErrorCode.ContainerNotFound)
        {
            throw new StoredContentMissingException($"Stored content {contentId.Value} has no blob in container '{clients.Container.Name}'.", exception);
        }
    }

    public async Task DeleteAsync(StoredContentId contentId, CancellationToken cancellationToken) =>
        await clients.Container.GetBlobClient(BlobName(contentId)).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken).ConfigureAwait(false);

    public static string BlobName(StoredContentId contentId) => contentId.Value.ToString("N");

    private sealed class BlobDocumentUpload(BlockStagingStream content) : IDocumentUpload
    {
        public Stream Content => content;

        public Task CommitAsync(CancellationToken cancellationToken) => content.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => content.DisposeAsync();
    }
}
