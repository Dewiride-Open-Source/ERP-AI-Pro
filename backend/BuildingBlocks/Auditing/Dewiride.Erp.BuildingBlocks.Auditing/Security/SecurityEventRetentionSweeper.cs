using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Security;

// It sweeps as the host starts and then once per interval, so a host restarted more often than the interval still deletes
// what is past the retention.
internal sealed partial class SecurityEventRetentionSweeper(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, IOptions<AuditingOptions> options, ILogger<SecurityEventRetentionSweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.SweepInterval, timeProvider);
        do
        {
            await SweepOnceAsync(stoppingToken).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    internal async Task SweepOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var cutoff = timeProvider.GetUtcNow() - options.Value.SecurityEventRetention;
            var deleted = await scope.ServiceProvider.GetRequiredService<AuditingDbContext>().SecurityEvents
                .Where(e => e.OccurredAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            LogSwept(deleted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} security events past their retention.")]
    private partial void LogSwept(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Deleting security events past their retention failed; the next sweep will retry.")]
    private partial void LogFailed(Exception exception);
}
