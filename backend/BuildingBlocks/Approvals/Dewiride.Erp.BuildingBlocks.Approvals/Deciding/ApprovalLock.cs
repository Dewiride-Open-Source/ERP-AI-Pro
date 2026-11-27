using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Deciding;

// A decision and a withdrawal read the request's state and write it back, so each first takes this lock of its transaction, before
// any row is written: two people deciding at once leave one decision, a module's withdrawal waits for a decision being saved and
// then sees the request it left, and the lock always comes before the locks of the record the outcome moves.
internal static class ApprovalLock
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static string ResourceOf(ApprovalRequestId id) => $"{ApprovalTables.Schema}.{id.Value:D}";

    public static Task<bool> TryAcquireAsync(DbContext context, ApprovalRequestId id, CancellationToken cancellationToken) =>
        TransactionLocks.TryAcquireExclusiveAsync(context, ResourceOf(id), Timeout, cancellationToken);
}
