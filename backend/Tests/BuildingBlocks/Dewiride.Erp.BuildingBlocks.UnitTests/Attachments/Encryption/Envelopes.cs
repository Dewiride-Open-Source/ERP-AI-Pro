using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;
using Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Fakes;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Encryption;

internal static class Envelopes
{
    public const int SegmentSize = EnvelopeHeader.ChunkSize + EnvelopeHeader.TagSize;

    public const int HeaderLengthWithoutKeyId = 93;

    public static int HeaderLength(EncryptionKey key) => HeaderLengthWithoutKeyId + key.Id.Length;

    public static int ChunkOffset(EncryptionKey key, int index) => HeaderLength(key) + (index * SegmentSize);

    public static async Task<byte[]> SealAsync(EncryptionKey key, StoredContentId contentId, byte[] plaintext, int writeSize, CancellationToken cancellationToken)
    {
        var dataKey = RandomNumberGenerator.GetBytes(EnvelopeHeader.DataKeySize);
        var header = EnvelopeHeader.Create(key, contentId, dataKey);
        using var destination = new MemoryStream();
        using (var writer = await EnvelopeWriter.StartAsync(destination, header, dataKey, cancellationToken))
        {
            for (var offset = 0; offset < plaintext.Length; offset += writeSize)
            {
                await writer.WriteAsync(plaintext.AsMemory(offset, Math.Min(writeSize, plaintext.Length - offset)), cancellationToken);
            }

            await writer.CompleteAsync(cancellationToken);
        }

        return destination.ToArray();
    }

    public static KeyRing Ring(EncryptionKey current, params EncryptionKey[] retired) =>
        new(new SettableOptionsMonitor<AttachmentsOptions>(new AttachmentsOptions
        {
            EncryptionKey = TestKeys.Setting(current),
            RetiredEncryptionKeys = TestKeys.RetiredSetting(retired),
        }));

    public static Task<EnvelopeReadStream> OpenAsync(byte[] envelope, KeyRing keys, StoredContentId contentId, long length, CancellationToken cancellationToken) =>
        EnvelopeReadStream.OpenAsync(new MemoryStream(envelope), keys, contentId, length, cancellationToken);

    public static async Task<byte[]> OpenAndReadAsync(byte[] envelope, KeyRing keys, StoredContentId contentId, long length, CancellationToken cancellationToken)
    {
        await using var stream = await OpenAsync(envelope, keys, contentId, length, cancellationToken);
        using var output = new MemoryStream();
        await CopyAsync(stream, output, 8191, cancellationToken);

        return output.ToArray();
    }

    public static async Task CopyAsync(Stream source, MemoryStream destination, int bufferSize, CancellationToken cancellationToken)
    {
        var buffer = new byte[bufferSize];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            destination.Write(buffer, 0, read);
        }
    }

    public static void Copy(Stream source, MemoryStream destination, int bufferSize)
    {
        var buffer = new byte[bufferSize];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            destination.Write(buffer, 0, read);
        }
    }

    public static byte[] Flip(byte[] envelope, int offset)
    {
        var tampered = (byte[])envelope.Clone();
        tampered[offset] ^= 0x01;

        return tampered;
    }
}
