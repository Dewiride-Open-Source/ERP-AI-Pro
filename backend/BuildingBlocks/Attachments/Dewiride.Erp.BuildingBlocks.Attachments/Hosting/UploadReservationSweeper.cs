using Dewiride.Erp.BuildingBlocks.Attachments.Persistence;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage;
using Dewiride.Erp.BuildingBlocks.Attachments.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Hosting;

// Works from this database's reservations, never from a blob listing, so several developers sharing one storage account
// never remove each other's files.
internal sealed partial class UploadReservationSweeper(
    IServiceScopeFactory scopes,
    IOptions<AttachmentsOptions> options,
    TimeProvider time,
    AttachmentsMetrics metrics,
    ILogger<UploadReservationSweeper> logger) : BackgroundService
{
    public const int BatchSize = 100;

    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AttachmentsDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var cutoff = time.GetUtcNow() - options.Value.UploadReservationLifetime;
        var expired = await context.UploadReservations
            .Where(r => r.ReservedAt < cutoff)
            .OrderBy(r => r.ReservedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var removed = 0;
        foreach (var reservation in expired)
        {
            if (!await context.StoredContents.AnyAsync(c => c.Id == reservation.ContentId, cancellationToken).ConfigureAwait(false))
            {
                await store.DeleteAsync(reservation.ContentId, cancellationToken).ConfigureAwait(false);
                removed++;
            }

            context.UploadReservations.Remove(reservation);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (removed > 0)
        {
            metrics.ReservationsSwept(removed);
        }

        return removed;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.ReservationSweepInterval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                var removed = await SweepOnceAsync(stoppingToken).ConfigureAwait(false);
                if (removed > 0)
                {
                    LogSwept(logger, removed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogSweepFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed the blobs of {Count} abandoned attachment uploads")]
    private static partial void LogSwept(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The attachment upload sweep failed; it runs again at the next interval")]
    private static partial void LogSweepFailed(ILogger logger, Exception exception);
}
