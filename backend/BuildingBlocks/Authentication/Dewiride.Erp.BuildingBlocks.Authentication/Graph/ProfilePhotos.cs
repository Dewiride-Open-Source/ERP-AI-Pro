namespace Dewiride.Erp.BuildingBlocks.Authentication.Graph;

// A photo is served as the type its own bytes show, never as the type Graph names, because the API sends nosniff and the
// browser then trusts that type. At most MaxBytes are read, the default largest entry of the in-process cache
// (Erp:Platform:Caching:MaximumPayloadBytes), so a photo the ERP shows can be cached.
internal static class ProfilePhotos
{
    public const int MaxBytes = 1024 * 1024;

    private const int ChunkBytes = 16 * 1024;

    public static string? ContentTypeOf(ReadOnlySpan<byte> content) => content switch
    {
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => "image/png",
        [0x47, 0x49, 0x46, 0x38, 0x37 or 0x39, 0x61, ..] => "image/gif",
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
        _ => null,
    };

    // Null when the photo is larger than MaxBytes, known before a byte is read when Graph sends the length.
    public static async Task<byte[]?> ReadAsync(HttpContent content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Headers.ContentLength > MaxBytes)
        {
            return null;
        }

        var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var photo = new MemoryStream();
            var chunk = new byte[ChunkBytes];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (photo.Length + read > MaxBytes)
                {
                    return null;
                }

                photo.Write(chunk, 0, read);
            }

            return photo.ToArray();
        }
    }
}
