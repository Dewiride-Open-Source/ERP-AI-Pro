using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Encryption;

// Layout (ADR-0022), integers big-endian: magic "ERPA" | version | key id length n | key id (n ASCII bytes) | content id (16) |
// chunk size (4) | chunk nonce prefix (7) | wrap nonce (12) | wrapped data key (32) | wrap tag (16). The data key is wrapped
// with AES-256-GCM under the key-encryption key with every byte before the wrap nonce as associated data, and every chunk
// authenticates the whole header, so neither the header nor a chunk can be moved to another file.
internal sealed class EnvelopeHeader
{
    public const byte FormatVersion = 1;

    public const int ChunkSize = 64 * 1024;

    public const int TagSize = 16;

    public const int NonceSize = 12;

    public const int DataKeySize = 32;

    public const int NoncePrefixSize = 7;

    private const int LeadSize = 6;

    private const int ContentIdSize = 16;

    private const int ChunkSizeFieldSize = 4;

    private const int TrailSize = ContentIdSize + ChunkSizeFieldSize + NoncePrefixSize + NonceSize + DataKeySize + TagSize;

    private readonly int _wrapOffset;

    private EnvelopeHeader(string keyId, StoredContentId contentId, byte[] bytes, int prefixOffset, int wrapOffset)
    {
        KeyId = keyId;
        ContentId = contentId;
        Bytes = bytes;
        NoncePrefix = bytes.AsMemory(prefixOffset, NoncePrefixSize);
        _wrapOffset = wrapOffset;
    }

    public static ReadOnlySpan<byte> Magic => "ERPA"u8;

    public string KeyId { get; }

    public StoredContentId ContentId { get; }

    public byte[] Bytes { get; }

    public ReadOnlyMemory<byte> NoncePrefix { get; }

    public static EnvelopeHeader Create(EncryptionKey key, StoredContentId contentId, ReadOnlySpan<byte> dataKey)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentOutOfRangeException.ThrowIfNotEqual(dataKey.Length, DataKeySize, nameof(dataKey));

        var keyId = Encoding.ASCII.GetBytes(key.Id);
        var bytes = new byte[LeadSize + keyId.Length + TrailSize];
        var span = bytes.AsSpan();
        Magic.CopyTo(span);
        span[4] = FormatVersion;
        span[5] = (byte)keyId.Length;
        keyId.CopyTo(span[LeadSize..]);
        var offset = LeadSize + keyId.Length;
        contentId.Value.TryWriteBytes(span.Slice(offset, ContentIdSize), bigEndian: true, out _);
        offset += ContentIdSize;
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(offset, ChunkSizeFieldSize), ChunkSize);
        offset += ChunkSizeFieldSize;
        var prefixOffset = offset;
        RandomNumberGenerator.Fill(span.Slice(offset, NoncePrefixSize));
        offset += NoncePrefixSize;
        var wrapOffset = offset;
        var wrapNonce = span.Slice(offset, NonceSize);
        RandomNumberGenerator.Fill(wrapNonce);
        using (var aes = new AesGcm(key.Material, TagSize))
        {
            aes.Encrypt(wrapNonce, dataKey, span.Slice(offset + NonceSize, DataKeySize), span.Slice(offset + NonceSize + DataKeySize, TagSize), span[..wrapOffset]);
        }

        return new EnvelopeHeader(key.Id, contentId, bytes, prefixOffset, wrapOffset);
    }

    public static async Task<EnvelopeHeader> ReadAsync(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        var lead = new byte[LeadSize];
        await ReadExactlyAsync(source, lead, cancellationToken).ConfigureAwait(false);
        if (!lead.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new EnvelopeFormatException("The stored file is not an attachment envelope.");
        }

        if (lead[4] != FormatVersion)
        {
            throw new EnvelopeFormatException($"The attachment envelope has version {lead[4]}; this build reads version {FormatVersion}.");
        }

        var keyIdLength = lead[5];
        if (keyIdLength is 0 or > EncryptionKey.MaxIdLength)
        {
            throw new EnvelopeFormatException("The attachment envelope carries a key id of an impossible length.");
        }

        var bytes = new byte[LeadSize + keyIdLength + TrailSize];
        lead.CopyTo(bytes, 0);
        await ReadExactlyAsync(source, bytes.AsMemory(LeadSize), cancellationToken).ConfigureAwait(false);
        var keyId = Encoding.ASCII.GetString(bytes, LeadSize, keyIdLength);
        if (!EncryptionKey.IsValidId(keyId))
        {
            throw new EnvelopeFormatException("The attachment envelope carries a malformed key id.");
        }

        var offset = LeadSize + keyIdLength;
        var contentId = StoredContentId.From(new Guid(bytes.AsSpan(offset, ContentIdSize), bigEndian: true));
        offset += ContentIdSize;
        if (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, ChunkSizeFieldSize)) != ChunkSize)
        {
            throw new EnvelopeFormatException($"The attachment envelope uses a chunk size other than {ChunkSize}.");
        }

        offset += ChunkSizeFieldSize;

        return new EnvelopeHeader(keyId, contentId, bytes, offset, offset + NoncePrefixSize);
    }

    public byte[] UnwrapDataKey(EncryptionKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var span = Bytes.AsSpan();
        var dataKey = new byte[DataKeySize];
        try
        {
            using var aes = new AesGcm(key.Material, TagSize);
            aes.Decrypt(span.Slice(_wrapOffset, NonceSize), span.Slice(_wrapOffset + NonceSize, DataKeySize), span.Slice(_wrapOffset + NonceSize + DataKeySize, TagSize), dataKey, span[.._wrapOffset]);
        }
        catch (AuthenticationTagMismatchException exception)
        {
            throw new EnvelopeFormatException($"The data key of the attachment envelope does not authenticate under key '{key.Id}'.", exception);
        }

        return dataKey;
    }

    public void WriteChunkNonce(Span<byte> nonce, uint index, bool last)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(nonce.Length, NonceSize, nameof(nonce));

        NoncePrefix.Span.CopyTo(nonce);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.Slice(NoncePrefixSize, sizeof(uint)), index);
        nonce[NonceSize - 1] = last ? (byte)1 : (byte)0;
    }

    private static async Task ReadExactlyAsync(Stream source, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            await source.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (EndOfStreamException exception)
        {
            throw new EnvelopeFormatException("The attachment envelope ends inside its header.", exception);
        }
    }
}
