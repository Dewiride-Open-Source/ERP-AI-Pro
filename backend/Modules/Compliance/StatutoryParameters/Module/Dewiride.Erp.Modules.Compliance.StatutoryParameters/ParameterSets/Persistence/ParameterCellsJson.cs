using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence;

// A row's cells are one JSON object of column name to canonical text, null for an empty optional column, never a JSON number,
// because the json column may round one. The text is written with only the escapes JSON requires, so a change record shows
// the words of a notification as they read; it is never placed in HTML unescaped.
internal static class ParameterCellsJson
{
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Write(ParameterCells cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            foreach (var (column, value) in cells.Values)
            {
                writer.WriteString(column, value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public static ParameterCells Read(string text)
    {
        using var document = JsonDocument.Parse(text);

        return ParameterCells.Of(document.RootElement.EnumerateObject().Select(cell => KeyValuePair.Create(
            cell.Name,
            cell.Value.ValueKind == JsonValueKind.Null ? null : cell.Value.GetString())));
    }
}
