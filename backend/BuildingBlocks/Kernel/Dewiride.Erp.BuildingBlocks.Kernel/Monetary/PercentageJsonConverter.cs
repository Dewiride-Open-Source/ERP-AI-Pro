using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

public sealed class PercentageJsonConverter : JsonConverter<Percentage>
{
    public override Percentage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Number
            ? new Percentage(reader.GetDecimal())
            : throw new JsonException("A percentage is a number of percent.");

    public override void Write(Utf8JsonWriter writer, Percentage value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteNumberValue(value.Value);
    }
}
