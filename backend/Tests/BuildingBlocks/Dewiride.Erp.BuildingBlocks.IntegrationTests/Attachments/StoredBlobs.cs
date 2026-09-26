using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob;
using Dewiride.Erp.Testing.Blob;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments;

internal static class StoredBlobs
{
    private static BlobContainerClient Container => BlobTestContainer.Current.Container;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Listing with uncommitted blobs is the only way to see blocks staged under a name that was never committed.
    public static async Task<BlobItem?> FindAnyAsync(StoredContentId contentId)
    {
        var name = BlobDocumentStore.BlobName(contentId);
        await foreach (var blob in Container.GetBlobsAsync(new GetBlobsOptions { States = BlobStates.Uncommitted, Prefix = name }, Cancellation))
        {
            if (blob.Name == name)
            {
                return blob;
            }
        }

        return null;
    }

    public static async Task<bool> IsCommittedAsync(StoredContentId contentId) =>
        (await Blob(contentId).ExistsAsync(Cancellation)).Value;

    public static async Task<byte[]> ReadAsync(StoredContentId contentId) =>
        (await Blob(contentId).DownloadContentAsync(Cancellation)).Value.Content.ToArray();

    public static async Task<long> LengthAsync(StoredContentId contentId) =>
        (await Blob(contentId).GetPropertiesAsync(cancellationToken: Cancellation)).Value.ContentLength;

    public static async Task<int> CommittedBlockCountAsync(StoredContentId contentId) =>
        (await Container.GetBlockBlobClient(BlobDocumentStore.BlobName(contentId)).GetBlockListAsync(BlockListTypes.Committed, cancellationToken: Cancellation)).Value.CommittedBlocks.Count();

    public static Task ReplaceAsync(StoredContentId contentId, byte[] content) =>
        Blob(contentId).UploadAsync(BinaryData.FromBytes(content), overwrite: true, Cancellation);

    public static Task DeleteAsync(StoredContentId contentId) =>
        Blob(contentId).DeleteAsync(cancellationToken: Cancellation);

    public static async Task StageBlockAsync(StoredContentId contentId, byte[] block)
    {
        using var content = new MemoryStream(block, writable: false);
        await Container.GetBlockBlobClient(BlobDocumentStore.BlobName(contentId))
            .StageBlockAsync(Convert.ToBase64String(new byte[sizeof(int)]), content, new BlockBlobStageBlockOptions(), Cancellation);
    }

    private static BlobClient Blob(StoredContentId contentId) => Container.GetBlobClient(BlobDocumentStore.BlobName(contentId));
}
