using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Encryption;

public sealed class EnvelopeTamperTests
{
    private const int Length = 3 * EnvelopeHeader.ChunkSize + 5;

    private readonly EncryptionKey _key = TestKeys.Create();

    private readonly StoredContentId _contentId = StoredContentId.Create();

    private readonly byte[] _plaintext = RandomNumberGenerator.GetBytes(Length);

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task OpenAsync_FlippedByteInTheLeadOrKeyId_ThrowsBeforeReturningAStream(int offset)
    {
        var envelope = await SealAsync();

        await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAsync(Envelopes.Flip(envelope, offset)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(26)]
    [InlineData(27)]
    [InlineData(38)]
    [InlineData(39)]
    [InlineData(70)]
    [InlineData(71)]
    [InlineData(86)]
    public async Task OpenAsync_FlippedByteAfterTheKeyId_ThrowsBeforeReturningAStream(int offsetAfterKeyId)
    {
        var envelope = await SealAsync();

        await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAsync(Envelopes.Flip(envelope, 6 + _key.Id.Length + offsetAfterKeyId)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(EnvelopeHeader.ChunkSize - 1)]
    [InlineData(EnvelopeHeader.ChunkSize)]
    [InlineData(Envelopes.SegmentSize - 1)]
    public async Task OpenAsync_DamagedFirstChunk_ThrowsBeforeReturningAStream(int offsetInChunk)
    {
        var envelope = await SealAsync();

        var exception = await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAsync(Envelopes.Flip(envelope, Envelopes.ChunkOffset(_key, 0) + offsetInChunk)));

        Assert.Equal("Chunk 0 of the attachment envelope does not authenticate.", exception.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ReadAsync_FlippedByteInALaterChunk_ThrowsAfterDeliveringOnlyTheChunksBeforeIt(int chunk)
    {
        var envelope = Envelopes.Flip(await SealAsync(), Envelopes.ChunkOffset(_key, chunk) + 2);
        await using var stream = await OpenAsync(envelope);
        using var delivered = new MemoryStream();

        var exception = await Assert.ThrowsAsync<EnvelopeFormatException>(() => Envelopes.CopyAsync(stream, delivered, 4096, TestContext.Current.CancellationToken));

        Assert.Equal($"Chunk {chunk} of the attachment envelope does not authenticate.", exception.Message);
        Assert.Equal(_plaintext[..(chunk * EnvelopeHeader.ChunkSize)], delivered.ToArray());
    }

    [Fact]
    public async Task Read_FlippedByteInAMiddleChunk_Throws()
    {
        var envelope = Envelopes.Flip(await SealAsync(), Envelopes.ChunkOffset(_key, 1) + 100);
        using var stream = await OpenAsync(envelope);
        using var delivered = new MemoryStream();

        Assert.Throws<EnvelopeFormatException>(() => Envelopes.Copy(stream, delivered, 4096));
        Assert.Equal(_plaintext[..EnvelopeHeader.ChunkSize], delivered.ToArray());
    }

    [Fact]
    public async Task ReadAsync_FlippedByteInTheLastTag_Throws()
    {
        var envelope = await SealAsync();

        await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAndReadAsync(Envelopes.Flip(envelope, envelope.Length - 1)));
    }

    [Fact]
    public async Task OpenAsync_FlippedByteInTheOnlyChunksTag_ThrowsBeforeReturningAStream()
    {
        var plaintext = RandomNumberGenerator.GetBytes(1000);
        var envelope = await Envelopes.SealAsync(_key, _contentId, plaintext, 1000, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAsync(Envelopes.Flip(envelope, envelope.Length - 1), length: plaintext.Length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ReadAsync_TruncatedAtAChunkBoundary_Throws(int chunksKept)
    {
        var envelope = await SealAsync();

        await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAndReadAsync(envelope[..Envelopes.ChunkOffset(_key, chunksKept)]));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, EnvelopeHeader.TagSize - 1)]
    [InlineData(0, Envelopes.SegmentSize - 1)]
    [InlineData(1, 70)]
    [InlineData(2, EnvelopeHeader.ChunkSize)]
    [InlineData(3, 5 + EnvelopeHeader.TagSize - 1)]
    public async Task ReadAsync_TruncatedInsideAChunk_Throws(int chunk, int bytesKept)
    {
        var envelope = await SealAsync();

        await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAndReadAsync(envelope[..(Envelopes.ChunkOffset(_key, chunk) + bytesKept)]));
    }

    [Theory]
    [InlineData(3 * 65536 + 5)]
    [InlineData(2 * 65536)]
    [InlineData(1)]
    public async Task ReadAsync_FinalChunkAppendedAgain_Throws(int length)
    {
        var plaintext = RandomNumberGenerator.GetBytes(length);
        var envelope = await Envelopes.SealAsync(_key, _contentId, plaintext, 7919, TestContext.Current.CancellationToken);
        var finalChunk = envelope[Envelopes.ChunkOffset(_key, (length - 1) / EnvelopeHeader.ChunkSize)..];

        await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAndReadAsync([.. envelope, .. finalChunk], length));
    }

    [Fact]
    public async Task ReadAsync_TrailingByteAppended_Throws()
    {
        var envelope = await SealAsync();

        await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAndReadAsync([.. envelope, 0x00]));
    }

    [Fact]
    public async Task ReadAsync_ChunkOfAnotherFileSwappedIn_Throws()
    {
        var envelope = await SealAsync();
        var other = await SealAsync();
        var offset = Envelopes.ChunkOffset(_key, 1);
        other.AsSpan(offset, Envelopes.SegmentSize).CopyTo(envelope.AsSpan(offset));

        await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAndReadAsync(envelope));
    }

    [Fact]
    public async Task OpenAsync_HeaderOfAnotherFileSwappedIn_ThrowsBeforeReturningAStream()
    {
        var envelope = await SealAsync();
        var other = await SealAsync();
        other.AsSpan(0, Envelopes.HeaderLength(_key)).CopyTo(envelope);

        await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAsync(envelope));
    }

    [Fact]
    public async Task ReadAsync_ChunksReordered_Throws()
    {
        var envelope = await SealAsync();
        var first = Envelopes.ChunkOffset(_key, 1);
        var second = Envelopes.ChunkOffset(_key, 2);
        var reordered = (byte[])envelope.Clone();
        envelope.AsSpan(first, Envelopes.SegmentSize).CopyTo(reordered.AsSpan(second));
        envelope.AsSpan(second, Envelopes.SegmentSize).CopyTo(reordered.AsSpan(first));

        await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAndReadAsync(reordered));
    }

    [Fact]
    public async Task OpenAsync_AnotherExpectedContentId_ThrowsNamingBoth()
    {
        var envelope = await SealAsync();
        var expected = StoredContentId.Create();

        var exception = await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAsync(envelope, contentId: expected));

        Assert.Equal($"The stored file belongs to content {_contentId.Value}, not {expected.Value}.", exception.Message);
    }

    [Theory]
    [InlineData(Length - 1)]
    [InlineData(Length + 1)]
    [InlineData(3 * 65536)]
    [InlineData(long.MaxValue)]
    public async Task ReadAsync_ExpectedLengthDiffers_Throws(long expectedLength)
    {
        var envelope = await SealAsync();

        var exception = await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAndReadAsync(envelope, expectedLength));

        Assert.Contains($"the {expectedLength} recorded", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65535)]
    public async Task OpenAsync_ExpectedLengthShorterThanTheFirstChunk_ThrowsBeforeReturningAStream(long expectedLength)
    {
        var envelope = await SealAsync();

        await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAsync(envelope, length: expectedLength));
    }

    [Theory]
    [InlineData(999)]
    [InlineData(1001)]
    public async Task OpenAsync_SingleChunkOfAnotherLength_ThrowsBeforeReturningAStream(long expectedLength)
    {
        var plaintext = RandomNumberGenerator.GetBytes(1000);
        var envelope = await Envelopes.SealAsync(_key, _contentId, plaintext, 1000, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAsync(envelope, length: expectedLength));
    }

    [Fact]
    public async Task OpenAsync_UnknownKeyId_ThrowsNamingIt()
    {
        var envelope = await SealAsync();

        var exception = await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAsync(envelope, Envelopes.Ring(TestKeys.Create(), TestKeys.Create())));

        Assert.Equal($"No configured attachment encryption key has the id '{_key.Id}'.", exception.Message);
    }

    [Fact]
    public async Task OpenAsync_KeyIdReusedWithOtherMaterial_ThrowsBeforeReturningAStream()
    {
        var envelope = await SealAsync();

        var exception = await Assert.ThrowsAsync<EnvelopeFormatException>(() => OpenAsync(envelope, Envelopes.Ring(TestKeys.Create(_key.Id))));

        Assert.Equal($"The data key of the attachment envelope does not authenticate under key '{_key.Id}'.", exception.Message);
    }

    private Task<byte[]> SealAsync() => Envelopes.SealAsync(_key, _contentId, _plaintext, 30011, TestContext.Current.CancellationToken);

    private Task<EnvelopeReadStream> OpenAsync(byte[] envelope, KeyRing? keys = null, StoredContentId? contentId = null, long length = Length) =>
        Envelopes.OpenAsync(envelope, keys ?? Envelopes.Ring(_key), contentId ?? _contentId, length, TestContext.Current.CancellationToken);

    private Task<byte[]> OpenAndReadAsync(byte[] envelope, long length = Length) =>
        Envelopes.OpenAndReadAsync(envelope, Envelopes.Ring(_key), _contentId, length, TestContext.Current.CancellationToken);
}
