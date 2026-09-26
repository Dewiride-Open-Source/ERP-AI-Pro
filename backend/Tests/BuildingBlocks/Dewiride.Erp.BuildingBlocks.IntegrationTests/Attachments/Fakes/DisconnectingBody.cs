namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Fakes;

// Behaves like the body of a request whose client goes away: after a given number of bytes the request is aborted, and the
// next read observes the aborted request's token, as a server's request body does.
internal sealed class DisconnectingBody(byte[] content, int abortAfter, CancellationTokenSource request) : Stream
{
    private int _position;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_position >= abortAfter)
        {
            await request.CancelAsync();
        }

        cancellationToken.ThrowIfCancellationRequested();
        var count = Math.Min(buffer.Length, Math.Min(content.Length, abortAfter) - _position);
        content.AsMemory(_position, count).CopyTo(buffer);
        _position += count;

        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
