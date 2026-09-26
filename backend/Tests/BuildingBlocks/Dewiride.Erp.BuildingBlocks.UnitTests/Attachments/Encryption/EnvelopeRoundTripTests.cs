using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;
using Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Fakes;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Encryption;

public sealed class EnvelopeRoundTripTests
{
    private readonly EncryptionKey _key = TestKeys.Create();

    private readonly StoredContentId _contentId = StoredContentId.Create();

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(65535, 4099, 8191)]
    [InlineData(65536, 65536, 65536)]
    [InlineData(65537, 1021, 65537)]
    [InlineData(3 * 65536, 131075, 3)]
    [InlineData(3 * 65536 + 5, 7919, 50000)]
    public async Task ReadAsync_SealedPlaintext_ReturnsEveryByte(int length, int writeSize, int readSize)
    {
        var plaintext = RandomNumberGenerator.GetBytes(length);
        var envelope = await Envelopes.SealAsync(_key, _contentId, plaintext, writeSize, TestContext.Current.CancellationToken);
        await using var stream = await Envelopes.OpenAsync(envelope, Envelopes.Ring(_key), _contentId, length, TestContext.Current.CancellationToken);
        using var output = new MemoryStream();

        await Envelopes.CopyAsync(stream, output, readSize, TestContext.Current.CancellationToken);

        Assert.Equal(plaintext, output.ToArray());
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(65535, 4099, 8191)]
    [InlineData(65536, 65536, 65536)]
    [InlineData(65537, 1021, 65537)]
    [InlineData(3 * 65536, 131075, 3)]
    [InlineData(3 * 65536 + 5, 7919, 50000)]
    public async Task Read_SealedPlaintext_ReturnsEveryByte(int length, int writeSize, int readSize)
    {
        var plaintext = RandomNumberGenerator.GetBytes(length);
        var envelope = await Envelopes.SealAsync(_key, _contentId, plaintext, writeSize, TestContext.Current.CancellationToken);
        using var stream = await Envelopes.OpenAsync(envelope, Envelopes.Ring(_key), _contentId, length, TestContext.Current.CancellationToken);
        using var output = new MemoryStream();

        Envelopes.Copy(stream, output, readSize);

        Assert.Equal(plaintext, output.ToArray());
    }

    [Fact]
    public async Task ReadAsync_SourceAnsweringWithShortReads_ReturnsEveryByte()
    {
        var plaintext = RandomNumberGenerator.GetBytes(3 * EnvelopeHeader.ChunkSize + 5);
        var envelope = await Envelopes.SealAsync(_key, _contentId, plaintext, 65000, TestContext.Current.CancellationToken);
        await using var stream = await EnvelopeReadStream.OpenAsync(new TrickleStream(envelope, 997), Envelopes.Ring(_key), _contentId, plaintext.Length, TestContext.Current.CancellationToken);
        using var output = new MemoryStream();

        await Envelopes.CopyAsync(stream, output, 4096, TestContext.Current.CancellationToken);

        Assert.Equal(plaintext, output.ToArray());
    }

    [Fact]
    public async Task Read_SourceAnsweringWithShortReads_ReturnsEveryByte()
    {
        var plaintext = RandomNumberGenerator.GetBytes(2 * EnvelopeHeader.ChunkSize + 1);
        var envelope = await Envelopes.SealAsync(_key, _contentId, plaintext, 65000, TestContext.Current.CancellationToken);
        using var stream = await EnvelopeReadStream.OpenAsync(new TrickleStream(envelope, 997), Envelopes.Ring(_key), _contentId, plaintext.Length, TestContext.Current.CancellationToken);
        using var output = new MemoryStream();

        Envelopes.Copy(stream, output, 4096);

        Assert.Equal(plaintext, output.ToArray());
    }

    [Fact]
    public async Task ReadAsync_EnvelopeSealedUnderARetiredKey_ReturnsEveryByte()
    {
        var retired = TestKeys.Create();
        var plaintext = RandomNumberGenerator.GetBytes(EnvelopeHeader.ChunkSize + 17);
        var envelope = await Envelopes.SealAsync(retired, _contentId, plaintext, 8192, TestContext.Current.CancellationToken);

        var read = await Envelopes.OpenAndReadAsync(envelope, Envelopes.Ring(_key, TestKeys.Create(), retired), _contentId, plaintext.Length, TestContext.Current.CancellationToken);

        Assert.Equal(plaintext, read);
    }

    [Fact]
    public async Task ReadAsync_EmptyBuffer_ReturnsZeroWithoutConsumingAnything()
    {
        var plaintext = RandomNumberGenerator.GetBytes(100);
        var envelope = await Envelopes.SealAsync(_key, _contentId, plaintext, 100, TestContext.Current.CancellationToken);
        await using var stream = await Envelopes.OpenAsync(envelope, Envelopes.Ring(_key), _contentId, plaintext.Length, TestContext.Current.CancellationToken);
        using var output = new MemoryStream();

        Assert.Equal(0, await stream.ReadAsync(Memory<byte>.Empty, TestContext.Current.CancellationToken));
        Assert.Equal(0, stream.Read(Span<byte>.Empty));
        await Envelopes.CopyAsync(stream, output, 64, TestContext.Current.CancellationToken);

        Assert.Equal(plaintext, output.ToArray());
    }

    [Fact]
    public async Task ReadAsync_AtTheEnd_KeepsReturningZero()
    {
        var plaintext = RandomNumberGenerator.GetBytes(EnvelopeHeader.ChunkSize);
        var envelope = await Envelopes.SealAsync(_key, _contentId, plaintext, 4096, TestContext.Current.CancellationToken);
        await using var stream = await Envelopes.OpenAsync(envelope, Envelopes.Ring(_key), _contentId, plaintext.Length, TestContext.Current.CancellationToken);
        using var output = new MemoryStream();
        await Envelopes.CopyAsync(stream, output, 4096, TestContext.Current.CancellationToken);
        var buffer = new byte[16];

        Assert.Equal(0, await stream.ReadAsync(buffer, TestContext.Current.CancellationToken));
        Assert.Equal(0, stream.Read(buffer, 0, buffer.Length));
    }

    [Fact]
    public async Task OpenAsync_OpenedStream_IsReadOnlyAndForwardOnly()
    {
        var envelope = await Envelopes.SealAsync(_key, _contentId, [1, 2, 3], 3, TestContext.Current.CancellationToken);
        await using var stream = await Envelopes.OpenAsync(envelope, Envelopes.Ring(_key), _contentId, 3, TestContext.Current.CancellationToken);

        Assert.True(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
        Assert.Throws<NotSupportedException>(() => stream.Write([1], 0, 1));
    }

    [Fact]
    public async Task DisposeAsync_Stream_DisposesTheSource()
    {
        var envelope = await Envelopes.SealAsync(_key, _contentId, [1, 2, 3], 3, TestContext.Current.CancellationToken);
        var source = new MemoryStream(envelope);
        var stream = await EnvelopeReadStream.OpenAsync(source, Envelopes.Ring(_key), _contentId, 3, TestContext.Current.CancellationToken);

        await stream.DisposeAsync();

        Assert.False(source.CanRead);
    }

    [Fact]
    public async Task Dispose_Stream_DisposesTheSource()
    {
        var envelope = await Envelopes.SealAsync(_key, _contentId, [1, 2, 3], 3, TestContext.Current.CancellationToken);
        var source = new MemoryStream(envelope);
        var stream = await EnvelopeReadStream.OpenAsync(source, Envelopes.Ring(_key), _contentId, 3, TestContext.Current.CancellationToken);

        stream.Dispose();

        Assert.False(source.CanRead);
    }
}
