using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Fakes;

// Tells a test when a command next asks SQL Server for an application lock, so a test can let a held decision go on only once
// another unit of work has read the request and is asking for the lock the decision holds.
internal sealed class LockRequestWatch : DbCommandInterceptor
{
    private TaskCompletionSource? _next;

    public Task NextRequest()
    {
        var next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Volatile.Write(ref _next, next);

        return next.Task;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.CommandText.Contains("sp_getapplock", StringComparison.Ordinal))
        {
            Interlocked.Exchange(ref _next, null)?.TrySetResult();
        }

        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }
}
