namespace Dewiride.Erp.BuildingBlocks.Idempotency.Http;

internal sealed class BoundedCaptureStream(Stream inner, int capacity) : Stream
{
    private readonly MemoryStream _captured = new();

    public bool Overflowed { get; private set; }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => inner.CanWrite;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public byte[]? Captured => Overflowed ? null : _captured.ToArray();

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        Capture(buffer.AsSpan(offset, count));
        inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Capture(buffer);
        inner.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Capture(buffer.AsSpan(offset, count));
        return inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Capture(buffer.Span);
        return inner.WriteAsync(buffer, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _captured.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Capture(ReadOnlySpan<byte> bytes)
    {
        if (Overflowed)
        {
            return;
        }

        if (_captured.Length + bytes.Length > capacity)
        {
            Overflowed = true;
            _captured.SetLength(0);
            return;
        }

        _captured.Write(bytes);
    }
}
