using Dewiride.Erp.BuildingBlocks.Attachments.Persistence;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage;
using Dewiride.Erp.BuildingBlocks.Attachments.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Hosting;

// Works from this database's rows, never from a blob listing, so several developers sharing one storage account never remove
// each other's files.
internal sealed partial class AttachmentsSweeper(
    IServiceScopeFactory scopes,
    IOptions<AttachmentsOptions> options,
    TimeProvider time,
    AttachmentsMetrics metrics,
    ILogger<AttachmentsSweeper> logger) : BackgroundService
{
    public const int BatchSize = 100;

    // A link followed in the moment before it expired records its redemption just after, so it outlives its expiry by this
    // much before an unredeemed link counts as unused.
    public static readonly TimeSpan LinkGracePeriod = TimeSpan.FromHours(1);

    public async Task<int> SweepReservationsAsync(CancellationToken cancellationToken)
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

    // A redeemed link stays for good: its redemptions, the record of who read the file, reference it.
    public async Task<int> PurgeUnusedLinksAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AttachmentsDbContext>();
        var cutoff = time.GetUtcNow() - LinkGracePeriod;
        var purged = await context.DownloadLinks
            .Where(l => l.ExpiresAt < cutoff && !context.DownloadRedemptions.Any(r => r.LinkId == l.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (purged > 0)
        {
            metrics.LinksPurged(purged);
        }

        return purged;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.SweepInterval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                var removed = await SweepReservationsAsync(stoppingToken).ConfigureAwait(false);
                var purged = await PurgeUnusedLinksAsync(stoppingToken).ConfigureAwait(false);
                if (removed > 0 || purged > 0)
                {
                    LogSwept(logger, removed, purged);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed the blobs of {Removed} abandoned attachment uploads and {Purged} unused download links")]
    private static partial void LogSwept(ILogger logger, int removed, int purged);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The attachments sweep failed; it runs again at the next interval")]
    private static partial void LogSweepFailed(ILogger logger, Exception exception);
}
