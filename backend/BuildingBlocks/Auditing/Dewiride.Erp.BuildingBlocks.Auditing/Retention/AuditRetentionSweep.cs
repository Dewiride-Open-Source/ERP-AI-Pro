using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Persistence.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Retention;

// A change record is kept whole financial years after the year of its change, counted in IST, so every record of one financial
// year leaves on the same day. Rows go in statements of at most BatchSize, each committed on its own, because a statement that
// takes 5,000 locks on one index makes SQL Server escalate to a lock on the whole table, which would hold back every insert of
// the running API until the statement ends. Every row a statement matches is past its retention, so it needs no ordering, and
// SQL Server receives a plain DELETE TOP.
internal sealed partial class AuditRetentionSweep(
    AuditingDbContext context,
    TimeProvider timeProvider,
    IOptions<AuditingOptions> options,
    ILogger<AuditRetentionSweep> logger) : IRetentionSweep
{
    public const int BatchSize = 1_000;

    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var settings = options.Value;

        var eventsBefore = now - settings.SecurityEventRetention;
        var events = await DeleteAsync(context.SecurityEvents.Where(e => e.OccurredAt < eventsBefore), cancellationToken).ConfigureAwait(false);
        LogSecurityEventsDeleted(logger, events, eventsBefore);

        if (settings.ChangeRecordRetentionYears is not { } years)
        {
            LogChangeRecordsKept(logger);
            return;
        }

        var recordsBefore = ChangeRecordsBefore(now, years);
        var records = await DeleteAsync(context.ChangeRecords.Where(r => r.OccurredAt < recordsBefore), cancellationToken).ConfigureAwait(false);
        LogChangeRecordsDeleted(logger, records, recordsBefore);
    }

    public static DateTimeOffset ChangeRecordsBefore(DateTimeOffset now, int years)
    {
        var current = FinancialYear.FromDate(IndianStandardTime.IstDate(now));

        return IndianStandardTime.StartOfIstDay(new FinancialYear(current.StartYear - years).Start);
    }

    private static async Task<long> DeleteAsync<TRow>(IQueryable<TRow> rows, CancellationToken cancellationToken)
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} security events recorded before {Cutoff}.")]
    private static partial void LogSecurityEventsDeleted(ILogger logger, long count, DateTimeOffset cutoff);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} change records made before {Cutoff}.")]
    private static partial void LogChangeRecordsDeleted(ILogger logger, long count, DateTimeOffset cutoff);

    [LoggerMessage(Level = LogLevel.Information, Message = "Kept every change record: no change record retention is configured.")]
    private static partial void LogChangeRecordsKept(ILogger logger);
}
