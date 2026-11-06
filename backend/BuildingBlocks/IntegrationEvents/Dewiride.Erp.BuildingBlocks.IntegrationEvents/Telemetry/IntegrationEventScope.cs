using System.Collections;
using System.Globalization;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Telemetry;

// The log scope of one delivery, inside the attempt's own scope. PublishedCorrelationId is the correlation id of the request
// whose change published the event, so a subscriber's lines lead back to the person's request, which the attempt's own
// CorrelationId, its trace id, does not.
internal sealed class IntegrationEventScope(IntegrationEventId eventId, string eventName, string source, string? publishedCorrelationId) : IReadOnlyList<KeyValuePair<string, object>>
{
    public const string EventIdProperty = "IntegrationEventId";

    public const string EventNameProperty = "IntegrationEventName";

    public const string SourceProperty = "IntegrationEventSource";

    public const string PublishedCorrelationIdProperty = "PublishedCorrelationId";

    public int Count => publishedCorrelationId is null ? 3 : 4;

    public KeyValuePair<string, object> this[int index] => index switch
    {
        0 => new KeyValuePair<string, object>(EventIdProperty, eventId.Value),
        1 => new KeyValuePair<string, object>(EventNameProperty, eventName),
        2 => new KeyValuePair<string, object>(SourceProperty, source),
        3 when publishedCorrelationId is not null => new KeyValuePair<string, object>(PublishedCorrelationIdProperty, publishedCorrelationId),
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{EventIdProperty}:{eventId.Value} {EventNameProperty}:{eventName} {SourceProperty}:{source} {PublishedCorrelationIdProperty}:{publishedCorrelationId}");
}
