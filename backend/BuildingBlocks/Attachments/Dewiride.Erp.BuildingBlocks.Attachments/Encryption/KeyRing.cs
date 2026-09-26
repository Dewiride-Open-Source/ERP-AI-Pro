using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Encryption;

internal sealed class KeyRing(IOptionsMonitor<AttachmentsOptions> options)
{
    private Snapshot? _snapshot;

    public EncryptionKey Current => Keys.Current;

    public IEnumerable<EncryptionKey> All => Keys.All;

    public EncryptionKey? Find(string id) => Keys.Find(id);

    // Parsed once per options instance, so a refreshed Key Vault value (a rotation) is picked up without a restart.
    private EncryptionKeySet Keys
    {
        get
        {
            var current = options.CurrentValue;
            var snapshot = _snapshot;
            if (snapshot is not null && ReferenceEquals(snapshot.Source, current))
            {
                return snapshot.Keys;
            }

            var keys = EncryptionKeys.Parse(current.EncryptionKey, current.RetiredEncryptionKeys);
            _snapshot = new Snapshot(current, keys);

            return keys;
        }
    }

    private sealed record Snapshot(AttachmentsOptions Source, EncryptionKeySet Keys);
}
