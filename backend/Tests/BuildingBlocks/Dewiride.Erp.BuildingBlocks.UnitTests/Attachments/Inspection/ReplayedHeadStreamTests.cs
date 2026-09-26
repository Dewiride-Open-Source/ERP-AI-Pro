using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Attachments.Inspection;
using Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Fakes;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Inspection;

public sealed class ReplayedHeadStreamTests
{
    private readonly byte[] _head = RandomNumberGenerator.GetBytes(10);

    private readonly byte[] _remainder = RandomNumberGenerator.GetBytes(100);

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(4096)]
    public void Read_AnyBufferSize_ReturnsTheHeadThenTheRemainder(int bufferSize)
    {
        using var stream = new ReplayedHeadStream(_head, new MemoryStream(_remainder));
        using var output = new MemoryStream();
        var buffer = new byte[bufferSize];

        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, read);
        }

        Assert.Equal([.. _head, .. _remainder], output.ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(4096)]
    public async Task ReadAsync_AnyBufferSize_ReturnsTheHeadThenTheRemainder(int bufferSize)
    {
        await using var stream = new ReplayedHeadStream(_head, new MemoryStream(_remainder));
        using var output = new MemoryStream();
        var buffer = new byte[bufferSize];

        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken)) > 0)
        {
            output.Write(buffer, 0, read);
        }

        Assert.Equal([.. _head, .. _remainder], output.ToArray());
    }

    [Fact]
    public async Task ReadAsync_ArrayOverloadWithAnOffset_FillsFromTheOffset()
    {
        await using var stream = new ReplayedHeadStream(_head, new MemoryStream(_remainder));
        var buffer = new byte[20];

#pragma warning disable CA1835 // The array overload is the behaviour under test.
        var read = await stream.ReadAsync(buffer, 5, 4, TestContext.Current.CancellationToken);
#pragma warning restore CA1835

        Assert.Equal(4, read);
        Assert.Equal(new byte[5], buffer[..5]);
        Assert.Equal(_head[..4], buffer[5..9]);
    }

    [Fact]
    public void Read_BufferLargerThanTheHead_ReturnsOnlyTheHeadAndLeavesTheRemainderUnread()
    {
        using var remainder = new MemoryStream(_remainder);
        using var stream = new ReplayedHeadStream(_head, remainder);
        var buffer = new byte[64];

        var read = stream.Read(buffer, 0, buffer.Length);

        Assert.Equal(_head.Length, read);
        Assert.Equal(_head, buffer[..read]);
        Assert.Equal(0, remainder.Position);
    }

    [Fact]
    public async Task ReadAsync_EmptyHead_ReadsTheRemainderDirectly()
    {
        await using var stream = new ReplayedHeadStream(ReadOnlyMemory<byte>.Empty, new TrickleStream(_remainder, 7));
        using var output = new MemoryStream();

        await stream.CopyToAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal(_remainder, output.ToArray());
    }

    [Fact]
    public void Dispose_Stream_LeavesTheRemainderOpen()
    {
        using var remainder = new MemoryStream(_remainder);
        var stream = new ReplayedHeadStream(_head, remainder);

        stream.Dispose();

        Assert.True(remainder.CanRead);
    }

    [Fact]
    public void Capabilities_AnyStream_AreReadOnlyAndForwardOnly()
    {
        using var stream = new ReplayedHeadStream(_head, new MemoryStream(_remainder));

        Assert.True(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
        Assert.Throws<NotSupportedException>(() => stream.Write([1], 0, 1));
    }
}
