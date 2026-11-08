using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;

public sealed class DayAndMonthJsonConverter : JsonConverter<DayAndMonth>
{
    public override DayAndMonth Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && DayAndMonth.TryParse(reader.GetString(), out var day)
            ? day
            : throw new JsonException("A day and month is written MM-dd, such as 04-01.");

    public override void Write(Utf8JsonWriter writer, DayAndMonth value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value.ToString());
    }
}
