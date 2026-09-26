namespace Dewiride.Erp.BuildingBlocks.Attachments.Scanning;

// A scanner receives the plaintext as it arrives, before anything is committed to storage, because the stored bytes are
// encrypted and cannot be scanned afterwards. When none is registered, attachments are recorded as not scanned.
public interface IAttachmentScanner
{
    ValueTask<IAttachmentScan> StartAsync(CancellationToken cancellationToken);
}
