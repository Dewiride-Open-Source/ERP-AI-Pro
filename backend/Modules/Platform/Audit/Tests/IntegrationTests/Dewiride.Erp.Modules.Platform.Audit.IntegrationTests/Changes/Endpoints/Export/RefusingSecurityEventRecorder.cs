using Dewiride.Erp.BuildingBlocks.Auditing.Security;

namespace Dewiride.Erp.Modules.Platform.Audit.IntegrationTests.Changes.Endpoints.Export;

// Stands in for an audit database that cannot take the record of an export.
internal sealed class RefusingSecurityEventRecorder : ISecurityEventRecorder
{
    public Task RecordAsync(SecurityEventEntry entry, CancellationToken cancellationToken) =>
        Task.FromException(new TimeoutException("The audit database did not answer."));
}
