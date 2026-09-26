using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Encryption;

public sealed class EnvelopeWriterTests
{
    private readonly EncryptionKey _key = TestKeys.Create();

    private readonly StoredContentId _contentId = StoredContentId.Create();

    [Theory]
    [InlineData(1, 1)]
    [InlineData(65535, 1)]
    [InlineData(65536, 1)]
    [InlineData(65537, 2)]
    [InlineData(3 * 65536, 3)]
    [InlineData(3 * 65536 + 5, 4)]
    public async Task CompleteAsync_Plaintext_WritesTheHeaderAndOneTaggedChunkPerStartedChunk(int length, int chunks)
    {
        var envelope = await Envelopes.SealAsync(_key, _contentId, RandomNumberGenerator.GetBytes(length), 3001, TestContext.Current.CancellationToken);

        Assert.Equal(93 + _key.Id.Length + length + (chunks * EnvelopeHeader.TagSize), envelope.Length);
    }

    [Fact]
    public async Task StartAsync_Header_IsWrittenFirstAndUnchanged()
    {
        var dataKey = RandomNumberGenerator.GetBytes(EnvelopeHeader.DataKeySize);
        var header = EnvelopeHeader.Create(_key, _contentId, dataKey);
        using var destination = new MemoryStream();

        using var writer = await EnvelopeWriter.StartAsync(destination, header, dataKey, TestContext.Current.CancellationToken);

        Assert.Equal(header.Bytes, destination.ToArray());
    }

    [Fact]
    public async Task CompleteAsync_SamePlaintextTwice_ProducesUnrelatedCiphertext()
    {
        var plaintext = new byte[EnvelopeHeader.ChunkSize];
        var chunk = Envelopes.HeaderLength(_key);

        var first = await Envelopes.SealAsync(_key, _contentId, plaintext, plaintext.Length, TestContext.Current.CancellationToken);
        var second = await Envelopes.SealAsync(_key, _contentId, plaintext, plaintext.Length, TestContext.Current.CancellationToken);

        Assert.NotEqual(first[chunk..], second[chunk..]);
        Assert.Equal(-1, first.AsSpan(chunk).IndexOf(new byte[64]));
    }

    [Fact]
    public async Task WriteAsync_AfterComplete_ThrowsObjectDisposedException()
    {
        var dataKey = RandomNumberGenerator.GetBytes(EnvelopeHeader.DataKeySize);
        using var destination = new MemoryStream();
        using var writer = await EnvelopeWriter.StartAsync(destination, EnvelopeHeader.Create(_key, _contentId, dataKey), dataKey, TestContext.Current.CancellationToken);
        await writer.WriteAsync(new byte[10], TestContext.Current.CancellationToken);
        await writer.CompleteAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await writer.WriteAsync(new byte[1], TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await writer.CompleteAsync(TestContext.Current.CancellationToken));
    }
}
