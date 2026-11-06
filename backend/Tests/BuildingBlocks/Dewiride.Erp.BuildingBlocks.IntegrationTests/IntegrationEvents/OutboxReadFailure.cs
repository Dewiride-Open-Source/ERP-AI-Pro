using System.Data.Common;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents;

// Fails every read of the sample outbox while a test has it on, before the read reaches the database, as the outbox of a module
// fails whose table a developer's database has not got yet.
internal sealed class OutboxReadFailure : DbCommandInterceptor
{
    private const string OutboxTable = $"[{SampleDbContext.SchemaName}].[{OutboxMessageConfiguration.TableName}]";

    public bool Failing { get; set; }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (Failing && command.CommandText.Contains(OutboxTable, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The outbox cannot be read.");
        }

        return ValueTask.FromResult(result);
    }
}
