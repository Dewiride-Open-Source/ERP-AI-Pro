namespace Dewiride.Erp.BuildingBlocks.Attachments.Service;

public sealed class AttachmentDownload(Stream content, string fileName, string contentType, long sizeBytes) : IAsyncDisposable
{
    public Stream Content { get; } = content;

    public string FileName { get; } = fileName;

    public string ContentType { get; } = contentType;

    public long SizeBytes { get; } = sizeBytes;

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
