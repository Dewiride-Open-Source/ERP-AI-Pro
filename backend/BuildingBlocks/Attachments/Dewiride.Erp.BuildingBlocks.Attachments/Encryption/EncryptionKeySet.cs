namespace Dewiride.Erp.BuildingBlocks.Attachments.Encryption;

internal sealed class EncryptionKeySet(EncryptionKey current, IReadOnlyList<EncryptionKey> retired)
{
    public EncryptionKey Current { get; } = current;

    public IReadOnlyList<EncryptionKey> Retired { get; } = retired;

    public IEnumerable<EncryptionKey> All => Retired.Prepend(Current);

    public EncryptionKey? Find(string id) => All.FirstOrDefault(key => string.Equals(key.Id, id, StringComparison.Ordinal));
}
