using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence;

// The columns are kept as the seed file declares them, without its references: a flag only when true, a bound as canonical
// text, so the json column never rounds it, and choices only for a choice column. Reading it back builds the same columns, so a
// reseed of equal columns changes nothing whatever text SQL Server returns. The text is written with only the escapes JSON
// requires, as a row's cells are.
internal static class ParameterColumnSetJson
{
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Write(ParameterColumnSet columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartArray();
            foreach (var column in columns.Columns)
            {
                writer.WriteStartObject();
                writer.WriteString("name", column.Name);
                writer.WriteString("title", column.Title);
                writer.WriteString("kind", JsonNamingPolicy.CamelCase.ConvertName(column.Kind.ToString()));
                WriteFlag(writer, "key", column.IsKey);
                WriteFlag(writer, "rowTitle", column.IsRowTitle);
                WriteFlag(writer, "optional", column.IsOptional);
                WriteBound(writer, "minimum", column.Minimum);
                WriteBound(writer, "maximum", column.Maximum);
                if (column.Kind == CellKind.Choice)
                {
                    writer.WriteStartArray("choices");
                    foreach (var choice in column.Choices)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("value", choice.Value);
                        writer.WriteString("title", choice.Title);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public static ParameterColumnSet Read(string text)
    {
        using var document = JsonDocument.Parse(text);

        return ParameterColumnSet.Create(document.RootElement.EnumerateArray().Select(column => new ParameterColumn(
            column.GetProperty("name").GetString()!,
            column.GetProperty("title").GetString()!,
            Enum.GetValues<CellKind>().Single(kind => string.Equals(JsonNamingPolicy.CamelCase.ConvertName(kind.ToString()), column.GetProperty("kind").GetString(), StringComparison.Ordinal)),
            column.TryGetProperty("key", out _),
            column.TryGetProperty("rowTitle", out _),
            column.TryGetProperty("optional", out _),
            Bound(column, "minimum"),
            Bound(column, "maximum"),
            column.TryGetProperty("choices", out var choices)
                ? [.. choices.EnumerateArray().Select(choice => new ParameterChoice(choice.GetProperty("value").GetString()!, choice.GetProperty("title").GetString()!))]
                : [])));
    }

    private static void WriteFlag(Utf8JsonWriter writer, string name, bool value)
    {
        if (value)
        {
            writer.WriteBoolean(name, true);
        }
    }

    private static void WriteBound(Utf8JsonWriter writer, string name, decimal? value)
    {
        if (value is { } bound)
        {
            writer.WriteString(name, CanonicalNumber.Of(bound));
        }
    }

    private static decimal? Bound(JsonElement column, string name) =>
        !column.TryGetProperty(name, out var bound)
            ? null
            : CanonicalNumber.TryParse(bound.GetString(), out var value) ? value : throw new FormatException($"The stored {name} '{bound.GetString()}' of a column is not a canonical number.");
}
