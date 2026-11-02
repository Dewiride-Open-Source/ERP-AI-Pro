using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Queue;

// Payloads use the HTTP API's JSON rules: web names, strict numbers and enums as camelCase names. The json column stores a
// document, not its text, so a stored payload is compared with a new one through the payload type: both written by this
// serializer, never as the text the database hands back.
internal static class JobPayloads
{
    public const int MaxBytes = 16 * 1024;

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize<TPayload>(TPayload payload)
        where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(payload);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, Options);
        if (bytes.Length > MaxBytes)
        {
            throw new ArgumentException($"A job payload is at most {MaxBytes} bytes of JSON; this {typeof(TPayload).Name} is {bytes.Length}. Keep identifiers only and read the data when the job runs.", nameof(payload));
        }

        return Encoding.UTF8.GetString(bytes);
    }

    public static bool TryDeserialize(string? json, Type payloadType, [NotNullWhen(true)] out object? payload)
    {
        ArgumentNullException.ThrowIfNull(payloadType);

        payload = null;
        if (json is null)
        {
            return false;
        }

        try
        {
            payload = JsonSerializer.Deserialize(json, payloadType, Options);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return false;
        }

        return payload is not null;
    }

    public static bool SameAs(string? stored, string? json, Type? payloadType)
    {
        if (payloadType is null || stored is null || json is null)
        {
            return stored is null && json is null;
        }

        return TryDeserialize(stored, payloadType, out var value)
            && string.Equals(Normalised(value, payloadType), json, StringComparison.Ordinal);
    }

    private static string Normalised(object value, Type payloadType) => JsonSerializer.Serialize(value, payloadType, Options);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { NumberHandling = JsonNumberHandling.Strict };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
