using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Kernel.Events;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;

// Events use the job payloads' rules: web names, strict numbers and enums as camelCase names, read strictly, so an event missing a
// constructor parameter that has no default, or holding null where its type allows none, is unreadable instead of being filled
// with made-up values; a member added later with a default reads as that default from an event published before it. Members the
// type does not know are skipped, so an event that a later release published still delivers after a rollback. An event is written
// as a delivery carries it, read back and written again, which escapes characters such as the plus of a time zone offset that the
// first writing leaves bare, and is at most MaxBytes in that form, leaving room for the delivery around it within a job payload's
// 16 KiB.
internal static class IntegrationEventSerializer
{
    public const int MaxBytes = 15 * 1024;

    internal static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize<TEvent>(TEvent integrationEvent)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(JsonSerializer.SerializeToElement(integrationEvent, Options), Options);
        if (bytes.Length > MaxBytes)
        {
            throw new ArgumentException($"An integration event is at most {MaxBytes} bytes of JSON; this {typeof(TEvent).Name} is {bytes.Length}. Keep identifiers, codes and dates, and let the subscriber read the rest.", nameof(integrationEvent));
        }

        return Encoding.UTF8.GetString(bytes);
    }

    public static bool TryDeserialize<TEvent>(JsonElement json, [NotNullWhen(true)] out TEvent? integrationEvent)
        where TEvent : class, IIntegrationEvent
    {
        try
        {
            integrationEvent = json.Deserialize<TEvent>(Options);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            integrationEvent = null;
        }

        return integrationEvent is not null;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.Strict,
            RespectRequiredConstructorParameters = true,
            RespectNullableAnnotations = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
