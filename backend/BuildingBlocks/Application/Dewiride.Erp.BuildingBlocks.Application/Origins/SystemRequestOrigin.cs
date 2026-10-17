using System.Diagnostics;

namespace Dewiride.Erp.BuildingBlocks.Application.Origins;

// Work the system does on its own, such as a hosted service or the migrator, comes from no client address; the trace it runs
// in, when there is one, ties its records to its logs and spans.
public sealed class SystemRequestOrigin : IRequestOrigin
{
    public string? ClientAddress => null;

    public string? CorrelationId => Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity ? activity.TraceId.ToHexString() : null;
}
