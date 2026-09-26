namespace Dewiride.Erp.BuildingBlocks.Attachments.Inspection;

// The upload's leading bytes are read once to check the file's type; this stream hands them out again ahead of the rest of
// the request body, which it does not own.
internal sealed class ReplayedHeadStream(ReadOnlyMemory<byte> head, Stream remainder) : Stream
{
    private ReadOnlyMemory<byte> _head = head;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        if (_head.IsEmpty)
        {
            return remainder.Read(buffer);
        }

        return TakeHead(buffer);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);

        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_head.IsEmpty)
        {
            return remainder.ReadAsync(buffer, cancellationToken);
        }

        return ValueTask.FromResult(TakeHead(buffer.Span));
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int TakeHead(Span<byte> buffer)
    {
        var count = Math.Min(buffer.Length, _head.Length);
        _head.Span[..count].CopyTo(buffer);
        _head = _head[count..];

        return count;
    }
}
