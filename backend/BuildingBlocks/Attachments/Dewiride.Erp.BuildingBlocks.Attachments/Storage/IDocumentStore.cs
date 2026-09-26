using Dewiride.Erp.BuildingBlocks.Attachments.Domain;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Storage;

// Holds envelope bytes only: encryption, hashing and scanning happen above it, so a store never sees plaintext.
internal interface IDocumentStore
{
    IDocumentUpload BeginUpload(StoredContentId contentId);

    Task<Stream> OpenReadAsync(StoredContentId contentId, CancellationToken cancellationToken);

    Task<Stream> OpenHeadAsync(StoredContentId contentId, int length, CancellationToken cancellationToken);

    Task DeleteAsync(StoredContentId contentId, CancellationToken cancellationToken);
}
