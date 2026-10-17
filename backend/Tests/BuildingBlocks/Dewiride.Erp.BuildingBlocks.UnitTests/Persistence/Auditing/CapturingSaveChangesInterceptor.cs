using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Auditing;

// Registered after the auditing interceptor, it sees the change records a save is about to insert and then stops the save, so a
// model without a database proves what the capture adds.
internal sealed class CapturingSaveChangesInterceptor : SaveChangesInterceptor
{
    public List<ChangeRecord> Records { get; } = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Records.AddRange(eventData.Context!.ChangeTracker.Entries<ChangeRecord>().Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity));

        return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
    }
}
