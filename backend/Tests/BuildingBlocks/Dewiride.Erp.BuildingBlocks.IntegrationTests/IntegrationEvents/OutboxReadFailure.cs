using System.Data.Common;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents;

// Fails the reads of the sample outbox from the one a test names on, counting from when it was armed and before the read reaches
// the database, as the outbox of a module fails whose table a developer's database has not got yet, or whose read times out in
// the middle of a pass.
internal sealed class OutboxReadFailure : DbCommandInterceptor
{
    private const string OutboxTable = $"[{SampleDbContext.SchemaName}].[{OutboxMessageConfiguration.TableName}]";

    private int _reads;

    private int? _failFrom;

    public void FailEveryRead() => FailFromRead(1);

    public void FailFromRead(int read)
    {
        _reads = 0;
        _failFrom = read;
    }

    public void Stop() => _failFrom = null;

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_failFrom is { } failFrom && command.CommandText.Contains(OutboxTable, StringComparison.Ordinal) && Interlocked.Increment(ref _reads) >= failFrom)
        {
            throw new InvalidOperationException("The outbox cannot be read.");
        }

        return ValueTask.FromResult(result);
    }
}
