using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Security;

// The request shares one AuditingDbContext, and a failed save leaves its entity tracked as added, so a later record of the
// same request would insert it after all: the record of a sign-in that started no session beside the failure that ended it.
// Every event leaves the context once its save has run, written or not.
internal sealed class SqlSecurityEventRecorder(AuditingDbContext context, TimeProvider timeProvider) : ISecurityEventRecorder
{
    public async Task RecordAsync(SecurityEventEntry entry, CancellationToken cancellationToken)
    {
        var added = context.SecurityEvents.Add(SecurityEvent.Record(entry, timeProvider.GetUtcNow()));
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            added.State = EntityState.Detached;
        }
    }
}
