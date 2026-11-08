using System.Text.Json;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;

// A text, code, choice, GST State code or date cell is a JSON string and an integer, percent or amount cell a JSON number, read
// exactly as a decimal and kept as its canonical text; null leaves an optional column empty.
internal static class SeedCells
{
    public static string? Read(ParameterColumn column, JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(column);

        var numeric = ParameterColumn.IsNumeric(column.Kind);
        var text = value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String when !numeric => value.GetString(),
            JsonValueKind.Number when numeric => value.TryGetDecimal(out var number)
                ? CanonicalNumber.Of(number)
                : throw new ArgumentException($"cell {column.Name}: {value.GetRawText()} is beyond the numbers the ERP holds.", nameof(value)),
            _ => throw new ArgumentException($"cell {column.Name}: a {column.Kind} cell is a JSON {(numeric ? "number" : "string")}, not {value.ValueKind}.", nameof(value)),
        };

        return column.Problem(text) is { } problem ? throw new ArgumentException($"cell {column.Name}: {problem}.", nameof(value)) : text;
    }
}
