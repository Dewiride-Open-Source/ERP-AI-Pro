using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Domain;

internal sealed class StoredContent : Entity<StoredContentId>
{
    public const int Sha256Length = 32;

    public const int KeyIdMaxLength = 32;

    private StoredContent(StoredContentId id)
        : base(id)
    {
    }

    public StoredContent(StoredContentId id, byte[] sha256, long length, string keyId, DateTimeOffset storedAt)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(sha256);
        ArgumentOutOfRangeException.ThrowIfNotEqual(sha256.Length, Sha256Length, nameof(sha256));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        Sha256 = sha256;
        Length = length;
        KeyId = keyId;
        StoredAt = storedAt;
    }

    public byte[] Sha256 { get; private set; } = [];

    public long Length { get; private set; }

    public string KeyId { get; private set; } = string.Empty;

    public DateTimeOffset StoredAt { get; private set; }
}
