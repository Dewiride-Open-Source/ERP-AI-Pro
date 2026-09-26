namespace Dewiride.Erp.BuildingBlocks.Attachments.Storage;

// Nothing written to Content becomes readable until CommitAsync; disposing without committing leaves no stored content.
internal interface IDocumentUpload : IAsyncDisposable
{
    Stream Content { get; }

    Task CommitAsync(CancellationToken cancellationToken);
}
