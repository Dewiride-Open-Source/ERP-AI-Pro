using System.Buffers;
using System.Collections;
using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes.Capture;

// A value entry is {"property", "kind", "before", "after", "references"}: a created row has no "before", a removed row no
// "after", and "references" names the change key of the recorded type a foreign key points at. A value is written the way the
// API writes it: an enum as its camelCase member name, an id as its Guid, a time in UTC. Every number travels as a string, so
// the json column, which may round a JSON number, keeps it exactly.
internal static class ChangeValueWriter
{
    private static readonly FrozenDictionary<ChangeValueKind, string> KindNames =
        Enum.GetValues<ChangeValueKind>().ToFrozenDictionary(kind => kind, kind => JsonNamingPolicy.CamelCase.ConvertName(kind.ToString()));

    // A decimal read back from a decimal(19,4) column carries four places and one set in code as many as it was written with,
    // so the trailing zeros are dropped and the same amount reads the same before and after; 28 places is a decimal's largest scale.
    private static readonly string DecimalFormat = "0." + new string('#', 28);

    public static string? Write(IReadOnlyList<ChangedValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
        {
            return null;
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var value in values)
            {
                writer.WriteStartObject();
                writer.WriteString("property", value.Property.Path);
                writer.WriteString("kind", KindNames[value.Property.Kind]);
                if (value.HasBefore)
                {
                    writer.WritePropertyName("before");
                    WriteValue(writer, value.Property, value.Before);
                }

                if (value.HasAfter)
                {
                    writer.WritePropertyName("after");
                    WriteValue(writer, value.Property, value.After);
                }

                if (value.Property.References is { } references)
                {
                    writer.WriteString("references", references);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteValue(Utf8JsonWriter writer, RecordedProperty property, object? value)
    {
        if (property.Kind == ChangeValueKind.List && value is IEnumerable elements and not string and not byte[])
        {
            var converter = property.Property.GetElementType()?.GetValueConverter();
            writer.WriteStartArray();
            foreach (var element in elements)
            {
                WriteScalar(writer, Writable(element, converter));
            }

            writer.WriteEndArray();
            return;
        }

        WriteScalar(writer, Writable(value, property.Property.GetValueConverter()));
    }

    private static object? Writable(object? value, ValueConverter? converter) =>
        value is null or Enum || IsScalar(value) || converter is null ? value : converter.ConvertToProvider(value);

    private static bool IsScalar(object value) =>
        value is string or bool or char or Guid or DateOnly or DateTimeOffset or DateTime or TimeOnly or TimeSpan or byte[]
            or decimal or double or float or Half or byte or sbyte or short or ushort or int or uint or long or ulong or Int128 or UInt128;

    private static void WriteScalar(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case Enum member:
                writer.WriteStringValue(Enum.IsDefined(member.GetType(), member) ? JsonNamingPolicy.CamelCase.ConvertName(member.ToString()) : member.ToString("D"));
                break;
            case Guid id:
                writer.WriteStringValue(id.ToString("D"));
                break;
            case DateOnly date:
                writer.WriteStringValue(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                break;
            case DateTimeOffset instant:
                writer.WriteStringValue(instant.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                break;
            case DateTime instant:
                writer.WriteStringValue(instant.ToString("O", CultureInfo.InvariantCulture));
                break;
            case TimeOnly time:
                writer.WriteStringValue(time.ToString("O", CultureInfo.InvariantCulture));
                break;
            case TimeSpan duration:
                writer.WriteStringValue(duration.ToString("c", CultureInfo.InvariantCulture));
                break;
            case byte[] bytes:
                writer.WriteBase64StringValue(bytes);
                break;
            case decimal number:
                writer.WriteStringValue(number.ToString(DecimalFormat, CultureInfo.InvariantCulture));
                break;
            case IFormattable formattable:
                writer.WriteStringValue(formattable.ToString(null, CultureInfo.InvariantCulture));
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }
}
