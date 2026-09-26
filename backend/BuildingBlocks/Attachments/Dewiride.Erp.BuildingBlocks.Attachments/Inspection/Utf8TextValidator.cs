using System.Text;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Inspection;

// Text has no signature, so the leading bytes alone cannot prove a file is text: every byte is checked as it streams past,
// and the decoder carries a character split across two reads into the next one.
internal sealed class Utf8TextValidator
{
    private const int CharacterBufferSize = 4096;

    private readonly Decoder _decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetDecoder();
    private readonly char[] _characters = new char[CharacterBufferSize];
    private bool _failed;

    public bool Append(ReadOnlySpan<byte> bytes)
    {
        if (_failed || bytes.Contains((byte)0))
        {
            return Fail();
        }

        try
        {
            while (!bytes.IsEmpty)
            {
                _decoder.Convert(bytes, _characters, flush: false, out var bytesUsed, out _, out _);
                bytes = bytes[bytesUsed..];
            }

            return true;
        }
        catch (DecoderFallbackException)
        {
            return Fail();
        }
    }

    public bool Complete()
    {
        if (_failed)
        {
            return false;
        }

        try
        {
            _decoder.Convert(ReadOnlySpan<byte>.Empty, _characters, flush: true, out _, out _, out _);

            return true;
        }
        catch (DecoderFallbackException)
        {
            return Fail();
        }
    }

    private bool Fail()
    {
        _failed = true;

        return false;
    }
}
