using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Idempotency.Http;

internal static class IdempotencyKeyHeader
{
    public const string Name = "Idempotency-Key";

    public const string ReplayedName = "Idempotency-Replayed";

    public const int MaxLength = 38;

    public static KeyParseResult Parse(IHeaderDictionary headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (!headers.TryGetValue(Name, out var values) || values.Count == 0)
        {
            return KeyParseResult.Missing;
        }

        if (values.Count != 1)
        {
            return KeyParseResult.Invalid;
        }

        var text = values[0].AsSpan().Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            text = text[1..^1];
        }

        return text.Length <= MaxLength && Guid.TryParseExact(text, "D", out var key) && key != Guid.Empty
            ? KeyParseResult.Valid(key)
            : KeyParseResult.Invalid;
    }
}
