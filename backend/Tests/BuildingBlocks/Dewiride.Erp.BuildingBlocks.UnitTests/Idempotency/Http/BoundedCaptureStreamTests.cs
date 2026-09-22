using System.Text;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Idempotency.Http;

public sealed class BoundedCaptureStreamTests
{
    [Fact]
    public async Task WriteAsync_WithinCapacity_ForwardsAndCapturesEveryByte()
    {
        using var inner = new MemoryStream();
        await using var capture = new BoundedCaptureStream(inner, 16);

        await capture.WriteAsync(Encoding.UTF8.GetBytes("hello "), TestContext.Current.CancellationToken);
        capture.Write(Encoding.UTF8.GetBytes("world"), 0, 5);
        await capture.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Equal("hello world", Encoding.UTF8.GetString(inner.ToArray()));
        Assert.Equal("hello world", Encoding.UTF8.GetString(capture.Captured!));
        Assert.False(capture.Overflowed);
    }

    [Fact]
    public async Task WriteAsync_BeyondCapacity_StillForwardsButDropsTheCapture()
    {
        using var inner = new MemoryStream();
        await using var capture = new BoundedCaptureStream(inner, 8);

        await capture.WriteAsync(Encoding.UTF8.GetBytes("12345"), TestContext.Current.CancellationToken);
        await capture.WriteAsync(Encoding.UTF8.GetBytes("6789"), TestContext.Current.CancellationToken);
        await capture.WriteAsync(Encoding.UTF8.GetBytes("0"), TestContext.Current.CancellationToken);

        Assert.Equal("1234567890", Encoding.UTF8.GetString(inner.ToArray()));
        Assert.True(capture.Overflowed);
        Assert.Null(capture.Captured);
    }

    [Fact]
    public async Task Read_AndSeek_AreNotSupported()
    {
        using var inner = new MemoryStream();
        await using var capture = new BoundedCaptureStream(inner, 8);

        Assert.False(capture.CanRead);
        Assert.False(capture.CanSeek);
        Assert.True(capture.CanWrite);
        Assert.Throws<NotSupportedException>(() => capture.Read(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => capture.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => capture.SetLength(0));
    }
}
