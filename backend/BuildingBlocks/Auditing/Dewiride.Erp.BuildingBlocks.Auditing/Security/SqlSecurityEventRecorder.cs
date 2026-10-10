using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Security;

internal sealed class SqlSecurityEventRecorder(AuditingDbContext context, TimeProvider timeProvider) : ISecurityEventRecorder
{
    public async Task RecordAsync(SecurityEventEntry entry, CancellationToken cancellationToken)
    {
        context.SecurityEvents.Add(SecurityEvent.Record(entry, timeProvider.GetUtcNow()));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
