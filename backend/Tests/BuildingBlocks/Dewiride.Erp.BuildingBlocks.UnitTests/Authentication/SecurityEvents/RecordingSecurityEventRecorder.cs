using Dewiride.Erp.BuildingBlocks.Auditing.Security;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.SecurityEvents;

internal sealed class RecordingSecurityEventRecorder : ISecurityEventRecorder
{
    public List<SecurityEventEntry> Entries { get; } = [];

    public IReadOnlySet<SecurityEventKind> Refused { get; init; } = new HashSet<SecurityEventKind>();

    public Exception Failure { get; init; } = new TimeoutException("The audit database did not answer.");

    public Task RecordAsync(SecurityEventEntry entry, CancellationToken cancellationToken)
    {
        if (Refused.Contains(entry.Kind))
        {
            return Task.FromException(Failure);
        }

        Entries.Add(entry);

        return Task.CompletedTask;
    }
}
