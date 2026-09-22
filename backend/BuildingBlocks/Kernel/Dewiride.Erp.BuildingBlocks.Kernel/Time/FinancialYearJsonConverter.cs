using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Time;

public sealed class FinancialYearJsonConverter : JsonConverter<FinancialYear>
{
    public override FinancialYear Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && FinancialYear.TryParse(reader.GetString(), CultureInfo.InvariantCulture, out var year)
            ? year
            : throw new JsonException("A financial year is a label of the form '2026-27'.");

    public override void Write(Utf8JsonWriter writer, FinancialYear value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value.Label);
    }
}
