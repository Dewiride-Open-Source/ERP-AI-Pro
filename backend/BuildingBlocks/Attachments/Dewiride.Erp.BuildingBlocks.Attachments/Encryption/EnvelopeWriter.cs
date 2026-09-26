using System.Security.Cryptography;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Encryption;

internal sealed class EnvelopeWriter : IDisposable
{
    private readonly Stream _destination;
    private readonly EnvelopeHeader _header;
    private readonly AesGcm _aes;
    private readonly byte[] _plaintext = new byte[EnvelopeHeader.ChunkSize];
    private readonly byte[] _ciphertext = new byte[EnvelopeHeader.ChunkSize + EnvelopeHeader.TagSize];
    private readonly byte[] _nonce = new byte[EnvelopeHeader.NonceSize];
    private int _buffered;
    private uint _index;
    private bool _completed;

    private EnvelopeWriter(Stream destination, EnvelopeHeader header, ReadOnlySpan<byte> dataKey)
    {
        _destination = destination;
        _header = header;
        _aes = new AesGcm(dataKey, EnvelopeHeader.TagSize);
    }

    public static async Task<EnvelopeWriter> StartAsync(Stream destination, EnvelopeHeader header, byte[] dataKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(dataKey);

        await destination.WriteAsync(header.Bytes, cancellationToken).ConfigureAwait(false);

        return new EnvelopeWriter(destination, header, dataKey);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_completed, this);

        while (!data.IsEmpty)
        {
            // A full chunk is emitted only once more data arrives, so the chunk that carries the last flag is always the final one.
            if (_buffered == _plaintext.Length)
            {
                await EmitAsync(last: false, cancellationToken).ConfigureAwait(false);
            }

            var take = Math.Min(_plaintext.Length - _buffered, data.Length);
            data[..take].CopyTo(_plaintext.AsMemory(_buffered));
            _buffered += take;
            data = data[take..];
        }
    }

    public async ValueTask CompleteAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_completed, this);

        await EmitAsync(last: true, cancellationToken).ConfigureAwait(false);
        _completed = true;
        await _destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _aes.Dispose();
        CryptographicOperations.ZeroMemory(_plaintext);
    }

    private async ValueTask EmitAsync(bool last, CancellationToken cancellationToken)
    {
        _header.WriteChunkNonce(_nonce, _index, last);
        _aes.Encrypt(_nonce, _plaintext.AsSpan(0, _buffered), _ciphertext.AsSpan(0, _buffered), _ciphertext.AsSpan(_buffered, EnvelopeHeader.TagSize), _header.Bytes);
        await _destination.WriteAsync(_ciphertext.AsMemory(0, _buffered + EnvelopeHeader.TagSize), cancellationToken).ConfigureAwait(false);
        _index = checked(_index + 1);
        _buffered = 0;
    }
}
