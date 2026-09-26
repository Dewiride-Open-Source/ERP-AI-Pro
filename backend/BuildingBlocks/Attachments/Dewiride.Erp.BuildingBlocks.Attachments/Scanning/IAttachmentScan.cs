namespace Dewiride.Erp.BuildingBlocks.Attachments.Scanning;

public interface IAttachmentScan : IAsyncDisposable
{
    ValueTask AppendAsync(ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken);

    ValueTask<AttachmentScanVerdict> CompleteAsync(CancellationToken cancellationToken);
}
