using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    private const string AmountProperty = "amount";

    private const string CurrencyProperty = "currency";

    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("A money value is an object with 'amount' and 'currency'.");
        }

        decimal? amount = null;
        Currency? currency = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            if (string.Equals(name, AmountProperty, StringComparison.OrdinalIgnoreCase))
            {
                amount = reader.TokenType == JsonTokenType.Number ? reader.GetDecimal() : throw new JsonException("'amount' is a number.");
            }
            else if (string.Equals(name, CurrencyProperty, StringComparison.OrdinalIgnoreCase))
            {
                currency = reader.TokenType == JsonTokenType.String && Currency.TryFromCode(reader.GetString(), out var parsed) ? parsed : throw new JsonException("'currency' is not a supported ISO 4217 code.");
            }
            else
            {
                reader.Skip();
            }
        }

        return amount is { } value && currency is { } code
            ? new Money(value, code)
            : throw new JsonException("A money value needs both 'amount' and 'currency'.");
    }

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteNumber(AmountProperty, value.Amount);
        writer.WriteString(CurrencyProperty, value.Currency.Code);
        writer.WriteEndObject();
    }
}
