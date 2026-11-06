using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;
using Dewiride.Erp.BuildingBlocks.Jobs;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Retention;

// An event leaves its outbox once it was passed on longer ago than PublishedEventRetention, never while it waits to be passed on,
// and a processed record leaves once it is older than the job runs it belongs to, so a delivery's record outlives no run that
// could re-run it. Both go oldest first in statements of BatchSize, each committed on its own, as the job runs' sweep does.
internal sealed partial class IntegrationEventRetentionSweep(
    IServiceProvider services,
    IntegrationEventTables tables,
    TimeProvider timeProvider,
    IOptions<IntegrationEventsOptions> options,
    IOptions<JobsOptions> jobsOptions,
    ILogger<IntegrationEventRetentionSweep> logger) : IRetentionSweep
{
    public const int BatchSize = 100;

    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var publishedCutoff = now - options.Value.PublishedEventRetention;
        foreach (var contextType in tables.Outboxes)
        {
            var context = (ModuleDbContext)services.GetRequiredService(contextType);
            var passedOn = context.Set<OutboxMessage>()
                .Where(m => m.RelayedAt != null && m.RelayedAt < publishedCutoff)
                .OrderBy(m => m.RelayedAt)
                .ThenBy(m => m.Id);
            var deleted = await DeleteAsync(passedOn, cancellationToken).ConfigureAwait(false);
            LogEventsDeleted(deleted, context.Schema, publishedCutoff);
        }

        var processedCutoff = now - jobsOptions.Value.FinishedRunRetention;
        foreach (var contextType in tables.ProcessedRecords)
        {
            var context = (ModuleDbContext)services.GetRequiredService(contextType);
            var processed = context.Set<ProcessedMessage>()
                .Where(m => m.ProcessedAt < processedCutoff)
                .OrderBy(m => m.ProcessedAt)
                .ThenBy(m => m.EventId)
                .ThenBy(m => m.Subscriber);
            var deleted = await DeleteAsync(processed, cancellationToken).ConfigureAwait(false);
            LogProcessedRecordsDeleted(deleted, context.Schema, processedCutoff);
        }
    }

    private static async Task<long> DeleteAsync<TEntity>(IOrderedQueryable<TEntity> rows, CancellationToken cancellationToken)
    {
        long total = 0;
        int deleted;
        do
        {
            deleted = await rows.Take(BatchSize).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            total += deleted;
        }
        while (deleted == BatchSize);

        return total;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} integration events of {Source} passed on before {Cutoff}.")]
    private partial void LogEventsDeleted(long count, string source, DateTimeOffset cutoff);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} processed-event records of {Source} older than {Cutoff}.")]
    private partial void LogProcessedRecordsDeleted(long count, string source, DateTimeOffset cutoff);
}
