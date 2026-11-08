using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;
using Dewiride.Erp.BuildingBlocks.Numbering.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Allocation;

// A number and a change of the numbering read the year's next number and write it back, so each first takes this lock of its
// transaction: the saves of one kind of document are numbered one after the other, and the lock ends when the document commits
// or rolls back, so a number not saved is the next save's.
internal static class NumberingLock
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static string ResourceOf(NumberedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return $"{NumberingTables.Schema}.{document.Key}";
    }

    public static Task<bool> TryAcquireAsync(DbContext context, NumberedDocument document, CancellationToken cancellationToken) =>
        TransactionLocks.TryAcquireExclusiveAsync(context, ResourceOf(document), Timeout, cancellationToken);
}
