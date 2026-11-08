using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.PublicApi;

// The canonical cells are written as the JSON of their kinds, a percent and an integer as numbers, an amount as Money in INR and
// every other kind as its text, a date's yyyy-MM-dd reading as DateOnly, and read into the row type as strictly as a seed file is read: a cell the type lacks, a parameter
// no cell fills, null where the type allows none or a choice the enum lacks is an error. A typed set and its seed file are held
// to each other by a test, so a row type that does not fit is a defect of the release.
internal sealed class TypedParameterRows(ParameterSet set, IReadOnlyList<ParameterRow> rows) : IStatutoryParameterRows
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public IReadOnlyList<TRow> Read<TRow>()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var row in rows)
            {
                writer.WriteStartObject();
                foreach (var column in set.Columns.Columns)
                {
                    writer.WritePropertyName(column.Name);
                    WriteCell(writer, column.Kind, row.Cells[column.Name]);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        try
        {
            return JsonSerializer.Deserialize<List<TRow>>(buffer.WrittenSpan, Options)
                ?? throw new InvalidOperationException($"The rows of {set.Key} read as no list of {typeof(TRow).Name}.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The rows of {set.Key} do not fit {typeof(TRow).Name}: {exception.Message}", exception);
        }
    }

    private static void WriteCell(Utf8JsonWriter writer, CellKind kind, string? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        switch (kind)
        {
            case CellKind.Integer or CellKind.Percent:
                writer.WriteNumberValue(Number(value));
                break;
            case CellKind.Amount:
                JsonSerializer.Serialize(writer, new Money(Number(value), Currency.Inr), Options);
                break;
            default:
                writer.WriteStringValue(value);
                break;
        }
    }

    private static decimal Number(string value) =>
        CanonicalNumber.TryParse(value, out var number) ? number : throw new InvalidOperationException($"The stored cell '{value}' is not a canonical number.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            NumberHandling = JsonNumberHandling.Strict,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowDuplicateProperties = false,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
