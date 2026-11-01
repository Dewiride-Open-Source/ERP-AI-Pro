using System.Collections;
using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Telemetry;

// The log scope of one attempt. CorrelationId is the property the HTTP pipeline's scope uses, so the logs of a request and of an
// attempt are found the same way, and its value is the attempt's trace id.
internal sealed class JobAttemptScope(JobRunId runId, string jobName, int attempt, string correlationId) : IReadOnlyList<KeyValuePair<string, object>>
{
    public const string RunIdProperty = "JobRunId";

    public const string JobNameProperty = "JobName";

    public const string AttemptProperty = "Attempt";

    public const string CorrelationIdProperty = "CorrelationId";

    public int Count => 4;

    public KeyValuePair<string, object> this[int index] => index switch
    {
        0 => new KeyValuePair<string, object>(RunIdProperty, runId.Value),
        1 => new KeyValuePair<string, object>(JobNameProperty, jobName),
        2 => new KeyValuePair<string, object>(AttemptProperty, attempt),
        3 => new KeyValuePair<string, object>(CorrelationIdProperty, correlationId),
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
        string.Create(CultureInfo.InvariantCulture, $"{RunIdProperty}:{runId.Value} {JobNameProperty}:{jobName} {AttemptProperty}:{attempt} {CorrelationIdProperty}:{correlationId}");
}
