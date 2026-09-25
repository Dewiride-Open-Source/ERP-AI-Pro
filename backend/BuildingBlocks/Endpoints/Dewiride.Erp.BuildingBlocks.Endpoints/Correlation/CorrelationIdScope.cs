using System.Collections;
using System.Globalization;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;

internal sealed class CorrelationIdScope(string correlationId) : IReadOnlyList<KeyValuePair<string, object>>
{
    public const string PropertyName = "CorrelationId";

    public int Count => 1;

    public KeyValuePair<string, object> this[int index] =>
        index == 0 ? new KeyValuePair<string, object>(PropertyName, correlationId) : throw new ArgumentOutOfRangeException(nameof(index));

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        yield return this[0];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{PropertyName}:{correlationId}");
}
