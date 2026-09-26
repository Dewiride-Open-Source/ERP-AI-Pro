using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Fakes;

// Wraps the real blob store: the content id of an upload that failed or was deduplicated is recorded nowhere else, and
// the tests need it to prove that no blob, committed or not, remains under that name.
internal sealed class RecordingDocumentStore(IDocumentStore inner) : IDocumentStore
{
    private readonly List<StoredContentId> _uploads = [];

    public IReadOnlyList<StoredContentId> Uploads => _uploads;

    public IDocumentUpload BeginUpload(StoredContentId contentId)
    {
        _uploads.Add(contentId);

        return inner.BeginUpload(contentId);
    }

    public Task<Stream> OpenReadAsync(StoredContentId contentId, CancellationToken cancellationToken) => inner.OpenReadAsync(contentId, cancellationToken);

    public Task DeleteAsync(StoredContentId contentId, CancellationToken cancellationToken) => inner.DeleteAsync(contentId, cancellationToken);
}
