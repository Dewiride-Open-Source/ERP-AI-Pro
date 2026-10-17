using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Fakes;

// Fails the first command whose text names the table before it reaches the database, with the TimeoutException that the SQL
// Server execution strategy treats as transient, so the strategy runs the save again.
public sealed class TransientFailureInterceptor(string table) : DbCommandInterceptor
{
    private int _armed = 1;

    public int Failures { get; private set; }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        FailOnce(command);

        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        FailOnce(command);

        return ValueTask.FromResult(result);
    }

    private void FailOnce(DbCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.CommandText.Contains(table, StringComparison.Ordinal) && Interlocked.Exchange(ref _armed, 0) == 1)
        {
            Failures++;
            throw new TimeoutException($"The command writing {table} timed out before it reached the database.");
        }
    }
}
