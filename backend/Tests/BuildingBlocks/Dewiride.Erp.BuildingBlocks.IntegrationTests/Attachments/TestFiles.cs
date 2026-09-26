using System.Security.Cryptography;
using System.Text;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments;

// Every file is unique to the test that makes it: identical bytes would be deduplicated onto another test's stored content,
// which that test may delete or corrupt, and which is encrypted under another factory's key.
internal static class TestFiles
{
    public const string PdfType = "application/pdf";

    public const string TextType = "text/plain";

    public static string Stamp() => Guid.CreateVersion7().ToString("N")[^12..];

    public static byte[] Pdf(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        "%PDF-"u8.CopyTo(bytes);

        return bytes;
    }

    public static byte[] Text(string marker, int length)
    {
        var line = Encoding.ASCII.GetBytes($"{marker} {Stamp()} is confidential\n");
        var bytes = new byte[length];
        for (var offset = 0; offset < length; offset += line.Length)
        {
            line.AsSpan(0, Math.Min(line.Length, length - offset)).CopyTo(bytes.AsSpan(offset));
        }

        return bytes;
    }
}
