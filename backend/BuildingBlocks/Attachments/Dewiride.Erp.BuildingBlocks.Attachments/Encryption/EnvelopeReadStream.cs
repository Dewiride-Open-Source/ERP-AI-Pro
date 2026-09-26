using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Encryption;

internal sealed class EnvelopeReadStream : Stream
{
    private const int SegmentSize = EnvelopeHeader.ChunkSize + EnvelopeHeader.TagSize;

    private readonly Stream _source;
    private readonly EnvelopeHeader _header;
    private readonly AesGcm _aes;
    private readonly long _expectedLength;
    private readonly byte[] _segment = new byte[SegmentSize + 1];
    private readonly byte[] _plaintext = new byte[EnvelopeHeader.ChunkSize];
    private readonly byte[] _nonce = new byte[EnvelopeHeader.NonceSize];
    private int _carried;
    private int _plaintextOffset;
    private int _plaintextLength;
    private uint _index;
    private long _produced;
    private bool _finished;

    private EnvelopeReadStream(Stream source, EnvelopeHeader header, byte[] dataKey, long expectedLength)
    {
        _source = source;
        _header = header;
        _aes = new AesGcm(dataKey, EnvelopeHeader.TagSize);
        _expectedLength = expectedLength;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    // The header, the data key and the first chunk are checked before the stream is handed out, so a damaged or foreign file
    // is refused before any byte reaches the caller; a later chunk that fails authentication throws from the read.
    public static async Task<EnvelopeReadStream> OpenAsync(Stream source, KeyRing keys, StoredContentId expectedContentId, long expectedLength, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keys);

        var header = await EnvelopeHeader.ReadAsync(source, cancellationToken).ConfigureAwait(false);
        if (header.ContentId != expectedContentId)
        {
            throw new EnvelopeFormatException($"The stored file belongs to content {header.ContentId.Value}, not {expectedContentId.Value}.");
        }

        var key = keys.Find(header.KeyId) ?? throw new EnvelopeFormatException($"No configured attachment encryption key has the id '{header.KeyId}'.");
        var dataKey = header.UnwrapDataKey(key);
        EnvelopeReadStream stream;
        try
        {
            stream = new EnvelopeReadStream(source, header, dataKey, expectedLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        try
        {
            stream.DecryptSegment(await stream.FillAsync(cancellationToken).ConfigureAwait(false));
        }
        catch
        {
            stream.ReleaseKey();
            throw;
        }

        return stream;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        while (_plaintextOffset == _plaintextLength)
        {
            if (_finished)
            {
                return 0;
            }

            DecryptSegment(Fill());
        }

        return Take(buffer);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);

        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        while (_plaintextOffset == _plaintextLength)
        {
            if (_finished)
            {
                return 0;
            }

            DecryptSegment(await FillAsync(cancellationToken).ConfigureAwait(false));
        }

        return Take(buffer.Span);
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async ValueTask DisposeAsync()
    {
        ReleaseKey();
        await _source.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ReleaseKey();
            _source.Dispose();
        }

        base.Dispose(disposing);
    }

    private int Take(Span<byte> buffer)
    {
        var count = Math.Min(buffer.Length, _plaintextLength - _plaintextOffset);
        _plaintext.AsSpan(_plaintextOffset, count).CopyTo(buffer);
        _plaintextOffset += count;

        return count;
    }

    // One byte beyond a full segment is read ahead: whether it exists is what tells a middle chunk from the final one.
    private int Fill()
    {
        var filled = _carried;
        int read;
        while (filled < _segment.Length && (read = _source.Read(_segment, filled, _segment.Length - filled)) > 0)
        {
            filled += read;
        }

        return filled;
    }

    private async ValueTask<int> FillAsync(CancellationToken cancellationToken)
    {
        var filled = _carried;
        int read;
        while (filled < _segment.Length && (read = await _source.ReadAsync(_segment.AsMemory(filled), cancellationToken).ConfigureAwait(false)) > 0)
        {
            filled += read;
        }

        return filled;
    }

    private void DecryptSegment(int filled)
    {
        var last = filled <= SegmentSize;
        var segmentLength = last ? filled : SegmentSize;
        if (segmentLength < EnvelopeHeader.TagSize)
        {
            throw new EnvelopeFormatException($"The attachment envelope ends inside chunk {_index}.");
        }

        var plaintextLength = segmentLength - EnvelopeHeader.TagSize;
        _header.WriteChunkNonce(_nonce, _index, last);
        try
        {
            _aes.Decrypt(_nonce, _segment.AsSpan(0, plaintextLength), _segment.AsSpan(plaintextLength, EnvelopeHeader.TagSize), _plaintext.AsSpan(0, plaintextLength), _header.Bytes);
        }
        catch (AuthenticationTagMismatchException exception)
        {
            throw new EnvelopeFormatException($"Chunk {_index} of the attachment envelope does not authenticate.", exception);
        }

        _produced += plaintextLength;
        if (_produced > _expectedLength || (last && _produced != _expectedLength))
        {
            throw new EnvelopeFormatException($"The attachment envelope holds a different number of bytes than the {_expectedLength} recorded for it.");
        }

        if (last)
        {
            _carried = 0;
            _finished = true;
        }
        else
        {
            _segment[0] = _segment[SegmentSize];
            _carried = 1;
            _index = checked(_index + 1);
        }

        _plaintextOffset = 0;
        _plaintextLength = plaintextLength;
    }

    private void ReleaseKey()
    {
        _aes.Dispose();
        CryptographicOperations.ZeroMemory(_plaintext);
    }
}
