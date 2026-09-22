using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Idempotency.Http;

internal static class RequestFingerprint
{
    private const byte Separator = (byte)'\n';

    public static async Task<byte[]> ComputeAsync(HttpRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, request.Method);
        Append(hash, request.Path.Value ?? string.Empty);
        Append(hash, request.QueryString.Value ?? string.Empty);
        Append(hash, actorId.ToString("D"));
        request.EnableBuffering();
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            hash.AppendData(buffer, 0, read);
        }

        request.Body.Position = 0;

        return hash.GetHashAndReset();
    }

    private static void Append(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData([Separator]);
    }
}
