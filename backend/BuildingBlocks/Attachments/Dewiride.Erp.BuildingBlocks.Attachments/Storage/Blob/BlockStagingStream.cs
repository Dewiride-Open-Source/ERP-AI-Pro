using System.Buffers;
using System.Buffers.Binary;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob;

// Stages every full buffer as an uncommitted block; the blob exists for readers only after CommitAsync puts the block list,
// and only if no blob of that name exists. Flush publishes nothing, so a writer that flushes cannot expose a partial file.
internal sealed class BlockStagingStream(BlockBlobClient blob) : Stream
{
    public const int BlockSize = 4 * 1024 * 1024;

    private const string ContentType = "application/octet-stream";

    private readonly List<string> _blockIds = [];
    private byte[]? _buffer = ArrayPool<byte>.Shared.Rent(BlockSize);
    private int _buffered;
    private bool _committed;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => _buffer is not null && !_committed;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var target = WritableBuffer();
        while (!buffer.IsEmpty)
        {
            var take = Math.Min(BlockSize - _buffered, buffer.Length);
            buffer[..take].CopyTo(target.AsMemory(_buffered));
            _buffered += take;
            buffer = buffer[take..];
            if (_buffered == BlockSize)
            {
                await StageAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);

        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Blocks are staged over the network; write asynchronously.");

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        WritableBuffer();
        if (_buffered > 0 || _blockIds.Count == 0)
        {
            await StageAsync(cancellationToken).ConfigureAwait(false);
        }

        await blob.CommitBlockListAsync(
            _blockIds,
            new CommitBlockListOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = ContentType },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
            },
            cancellationToken).ConfigureAwait(false);
        _committed = true;
        ReturnBuffer();
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ReturnBuffer();
        }

        base.Dispose(disposing);
    }

    // Every id of one blob must have the same length, so the index is encoded as four big-endian bytes.
    private static string BlockId(int index)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, index);

        return Convert.ToBase64String(bytes);
    }

    private async Task StageAsync(CancellationToken cancellationToken)
    {
        var blockId = BlockId(_blockIds.Count);
        using var content = new MemoryStream(_buffer!, 0, _buffered, writable: false);
        await blob.StageBlockAsync(
            blockId,
            content,
            new BlockBlobStageBlockOptions { TransferValidation = new UploadTransferValidationOptions { ChecksumAlgorithm = StorageChecksumAlgorithm.MD5 } },
            cancellationToken).ConfigureAwait(false);
        _blockIds.Add(blockId);
        _buffered = 0;
    }

    private byte[] WritableBuffer()
    {
        if (_committed)
        {
            throw new InvalidOperationException("The upload is already committed.");
        }

        return _buffer ?? throw new ObjectDisposedException(nameof(BlockStagingStream));
    }

    private void ReturnBuffer()
    {
        if (_buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }
    }
}
