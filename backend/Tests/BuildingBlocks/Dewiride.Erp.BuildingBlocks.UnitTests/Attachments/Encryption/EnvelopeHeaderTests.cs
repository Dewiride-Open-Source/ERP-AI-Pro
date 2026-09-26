using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Encryption;

public sealed class EnvelopeHeaderTests
{
    private readonly EncryptionKey _key = TestKeys.Create();

    private readonly StoredContentId _contentId = StoredContentId.Create();

    private readonly byte[] _dataKey = RandomNumberGenerator.GetBytes(EnvelopeHeader.DataKeySize);

    [Fact]
    public void Create_Header_StartsWithTheMagicTheVersionAndTheLengthPrefixedKeyId()
    {
        var bytes = EnvelopeHeader.Create(_key, _contentId, _dataKey).Bytes;

        Assert.Equal("ERPA"u8.ToArray(), bytes[..4]);
        Assert.Equal(1, bytes[4]);
        Assert.Equal(_key.Id.Length, bytes[5]);
        Assert.Equal(Encoding.ASCII.GetBytes(_key.Id), bytes[6..(6 + _key.Id.Length)]);
    }

    [Fact]
    public void Create_Header_CarriesTheContentIdBigEndianAfterTheKeyId()
    {
        var bytes = EnvelopeHeader.Create(_key, _contentId, _dataKey).Bytes;
        var offset = 6 + _key.Id.Length;

        Assert.Equal(Convert.FromHexString(_contentId.Value.ToString("N")), bytes[offset..(offset + 16)]);
    }

    [Fact]
    public void Create_Header_DeclaresTheChunkSizeBigEndianAfterTheContentId()
    {
        var bytes = EnvelopeHeader.Create(_key, _contentId, _dataKey).Bytes;
        var offset = 6 + _key.Id.Length + 16;

        Assert.Equal(new byte[] { 0x00, 0x01, 0x00, 0x00 }, bytes[offset..(offset + 4)]);
        Assert.Equal(65536, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(32)]
    public void Create_KeyIdLength_MakesTheHeaderNinetyThreeBytesPlusTheKeyId(int keyIdLength)
    {
        var key = TestKeys.Create(new string('k', keyIdLength));

        var header = EnvelopeHeader.Create(key, _contentId, _dataKey);

        Assert.Equal(93 + keyIdLength, header.Bytes.Length);
        Assert.Equal(EnvelopeHeader.NoncePrefixSize, header.NoncePrefix.Length);
    }

    [Fact]
    public void Create_SameKeyContentAndDataKeyTwice_DrawsFreshNoncesAndWrapsTheDataKeyDifferently()
    {
        var first = EnvelopeHeader.Create(_key, _contentId, _dataKey).Bytes;
        var second = EnvelopeHeader.Create(_key, _contentId, _dataKey).Bytes;
        var fixedPart = 6 + _key.Id.Length + 16 + 4;

        Assert.Equal(first[..fixedPart], second[..fixedPart]);
        Assert.NotEqual(first[fixedPart..(fixedPart + 7)], second[fixedPart..(fixedPart + 7)]);
        Assert.NotEqual(first[(fixedPart + 7)..(fixedPart + 19)], second[(fixedPart + 7)..(fixedPart + 19)]);
        Assert.NotEqual(first[(fixedPart + 19)..(fixedPart + 51)], second[(fixedPart + 19)..(fixedPart + 51)]);
    }

    [Fact]
    public void Create_Header_NeverHoldsTheDataKeyInTheClear()
    {
        var bytes = EnvelopeHeader.Create(_key, _contentId, _dataKey).Bytes;

        Assert.Equal(-1, bytes.AsSpan().IndexOf(_dataKey));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public void Create_DataKeyOfAnotherLength_Throws(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EnvelopeHeader.Create(_key, _contentId, new byte[length]));
    }

    [Fact]
    public async Task ReadAsync_CreatedHeader_ReturnsItsKeyIdContentIdAndBytes()
    {
        var created = EnvelopeHeader.Create(_key, _contentId, _dataKey);
        using var source = new MemoryStream([.. created.Bytes, .. "chunk"u8]);

        var read = await EnvelopeHeader.ReadAsync(source, TestContext.Current.CancellationToken);

        Assert.Equal(_key.Id, read.KeyId);
        Assert.Equal(_contentId, read.ContentId);
        Assert.Equal(created.Bytes, read.Bytes);
        Assert.Equal(created.NoncePrefix.ToArray(), read.NoncePrefix.ToArray());
        Assert.Equal(created.Bytes.Length, source.Position);
    }

    [Fact]
    public async Task ReadAsync_NotAnEnvelope_Throws()
    {
        using var source = new MemoryStream([.. "%PDF-1.7"u8, .. new byte[200]]);

        var exception = await Assert.ThrowsAsync<EnvelopeFormatException>(() => EnvelopeHeader.ReadAsync(source, TestContext.Current.CancellationToken));

        Assert.Equal("The stored file is not an attachment envelope.", exception.Message);
    }

    [Fact]
    public async Task ReadAsync_UnknownVersion_ThrowsNamingIt()
    {
        var bytes = EnvelopeHeader.Create(_key, _contentId, _dataKey).Bytes;
        bytes[4] = 2;

        var exception = await Assert.ThrowsAsync<EnvelopeFormatException>(() => EnvelopeHeader.ReadAsync(new MemoryStream(bytes), TestContext.Current.CancellationToken));

        Assert.Contains("version 2", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(33)]
    [InlineData(255)]
    public async Task ReadAsync_ImpossibleKeyIdLength_Throws(int length)
    {
        var bytes = EnvelopeHeader.Create(_key, _contentId, _dataKey).Bytes;
        bytes[5] = (byte)length;

        await Assert.ThrowsAsync<EnvelopeFormatException>(() => EnvelopeHeader.ReadAsync(new MemoryStream(bytes), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData((byte)' ')]
    [InlineData((byte)'/')]
    [InlineData((byte)0x00)]
    [InlineData((byte)0xE9)]
    public async Task ReadAsync_MalformedKeyId_Throws(byte character)
    {
        var bytes = EnvelopeHeader.Create(_key, _contentId, _dataKey).Bytes;
        bytes[6] = character;

        var exception = await Assert.ThrowsAsync<EnvelopeFormatException>(() => EnvelopeHeader.ReadAsync(new MemoryStream(bytes), TestContext.Current.CancellationToken));

        Assert.Equal("The attachment envelope carries a malformed key id.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32 * 1024)]
    [InlineData(64 * 1024 + 1)]
    public async Task ReadAsync_OtherChunkSize_Throws(int chunkSize)
    {
        var bytes = EnvelopeHeader.Create(_key, _contentId, _dataKey).Bytes;
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(6 + _key.Id.Length + 16, 4), chunkSize);

        await Assert.ThrowsAsync<EnvelopeFormatException>(() => EnvelopeHeader.ReadAsync(new MemoryStream(bytes), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(40)]
    [InlineData(-1)]
    public async Task ReadAsync_TruncatedHeader_ThrowsWrappingTheEndOfStream(int keep)
    {
        var bytes = EnvelopeHeader.Create(_key, _contentId, _dataKey).Bytes;
        var truncated = bytes[..(keep < 0 ? bytes.Length + keep : keep)];

        var exception = await Assert.ThrowsAsync<EnvelopeFormatException>(() => EnvelopeHeader.ReadAsync(new MemoryStream(truncated), TestContext.Current.CancellationToken));

        Assert.Equal("The attachment envelope ends inside its header.", exception.Message);
        Assert.IsType<EndOfStreamException>(exception.InnerException);
    }

    [Fact]
    public async Task UnwrapDataKey_TheWrappingKey_ReturnsTheDataKey()
    {
        var header = await EnvelopeHeader.ReadAsync(new MemoryStream(EnvelopeHeader.Create(_key, _contentId, _dataKey).Bytes), TestContext.Current.CancellationToken);

        Assert.Equal(_dataKey, header.UnwrapDataKey(_key));
    }

    [Fact]
    public void UnwrapDataKey_AnotherKey_ThrowsNamingThatKey()
    {
        var other = TestKeys.Create();
        var header = EnvelopeHeader.Create(_key, _contentId, _dataKey);

        var exception = Assert.Throws<EnvelopeFormatException>(() => header.UnwrapDataKey(other));

        Assert.Contains($"'{other.Id}'", exception.Message, StringComparison.Ordinal);
        Assert.IsType<AuthenticationTagMismatchException>(exception.InnerException);
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(1u, true)]
    [InlineData(0x01020304u, false)]
    [InlineData(uint.MaxValue, true)]
    public void WriteChunkNonce_IndexAndLastFlag_FollowTheNoncePrefix(uint index, bool last)
    {
        var header = EnvelopeHeader.Create(_key, _contentId, _dataKey);
        var nonce = new byte[EnvelopeHeader.NonceSize];
        var expectedIndex = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(expectedIndex, index);

        header.WriteChunkNonce(nonce, index, last);

        Assert.Equal(header.NoncePrefix.ToArray(), nonce[..7]);
        Assert.Equal(expectedIndex, nonce[7..11]);
        Assert.Equal(last ? 1 : 0, nonce[11]);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(13)]
    public void WriteChunkNonce_BufferOfAnotherLength_Throws(int length)
    {
        var header = EnvelopeHeader.Create(_key, _contentId, _dataKey);

        Assert.Throws<ArgumentOutOfRangeException>(() => header.WriteChunkNonce(new byte[length], 0, last: false));
    }
}
