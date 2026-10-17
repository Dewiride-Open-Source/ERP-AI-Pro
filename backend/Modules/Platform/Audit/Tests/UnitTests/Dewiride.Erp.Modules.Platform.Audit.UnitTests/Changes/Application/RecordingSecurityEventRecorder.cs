using Dewiride.Erp.BuildingBlocks.Auditing.Security;

namespace Dewiride.Erp.Modules.Platform.Audit.UnitTests.Changes.Application;

internal sealed class RecordingSecurityEventRecorder : ISecurityEventRecorder
{
    public List<SecurityEventEntry> Entries { get; } = [];

    public Exception? Failure { get; init; }

    public Task RecordAsync(SecurityEventEntry entry, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        Entries.Add(entry);

        return Task.CompletedTask;
    }
}
