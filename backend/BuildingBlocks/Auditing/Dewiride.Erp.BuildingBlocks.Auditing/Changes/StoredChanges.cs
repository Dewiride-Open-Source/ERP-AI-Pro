using System.Buffers;
using System.Collections.Frozen;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Changes;

// Reads the value entries the change capture writes, {"property", "kind", "before", "after", "references"}, where a creation
// has no "before" and a removal no "after". The kind is matched to its camelCase name exactly, as the capture writes it, so a
// misspelt or unknown kind is refused instead of read as another. A value that is not a string is kept as compact JSON with
// letters of every script left unescaped, so a list of names reads as the names.
internal static class StoredChanges
{
    private static readonly FrozenDictionary<string, ChangeValueKind> Kinds =
        Enum.GetValues<ChangeValueKind>().ToFrozenDictionary(kind => JsonNamingPolicy.CamelCase.ConvertName(kind.ToString()), StringComparer.Ordinal);

    private static readonly JsonWriterOptions CompactJson = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    public static IReadOnlyList<StoredChange> Parse(string? changes)
    {
        if (changes is null)
        {
            return [];
        }

        using var document = JsonDocument.Parse(changes);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The values of a change record are a JSON array.");
        }

        var parsed = new List<StoredChange>(document.RootElement.GetArrayLength());
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var kind = Text(entry, "kind");
            parsed.Add(new StoredChange(
                Text(entry, "property"),
                Kinds.TryGetValue(kind, out var known) ? known : throw new JsonException($"'{kind}' is not a kind of change record value."),
                Value(entry, "before"),
                Value(entry, "after"),
                OptionalText(entry, "references")));
        }

        return parsed;
    }

    private static string Text(JsonElement entry, string name) =>
        entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new JsonException($"A change record value has no '{name}' text.");

    private static string? OptionalText(JsonElement entry, string name) =>
        !entry.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null
            ? null
            : value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : throw new JsonException($"The '{name}' of a change record value is text.");

    private static string? Value(JsonElement entry, string name)
    {
        if (!entry.TryGetProperty(name, out var value))
        {
            return null;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.String:
                return value.GetString();
            default:
                var buffer = new ArrayBufferWriter<byte>();
                using (var writer = new Utf8JsonWriter(buffer, CompactJson))
                {
                    value.WriteTo(writer);
                }

                return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }
    }
}
