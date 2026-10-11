using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;

// A rule that reads rows other writes change, such as one over a whole tree, holds only while those writes are checked one
// after the other, so such a write first takes an application lock of its transaction. The lock ends with the transaction,
// committed or rolled back, and a retry of the unit of work takes it again. sp_getapplock answers 0 or 1 when it grants the
// lock and a negative number when it does not: a timeout, a cancellation, a deadlock victim or a parameter error.
public static class TransactionLocks
{
    public const int ResourceMaxLength = 255;

    public static async Task<bool> TryAcquireExclusiveAsync(DbContext context, string resource, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(resource.Length, ResourceMaxLength, nameof(resource));
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);

        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("An application lock belongs to a transaction; begin one first, as the unit of work does.");
        }

        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        await context.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = @timeout",
            [
                result,
                new SqlParameter("@resource", SqlDbType.NVarChar, ResourceMaxLength) { Value = resource },
                new SqlParameter("@timeout", SqlDbType.Int) { Value = (int)Math.Min(timeout.TotalMilliseconds, int.MaxValue) },
            ],
            cancellationToken).ConfigureAwait(false);

        return result.Value is int granted && granted >= 0;
    }
}
