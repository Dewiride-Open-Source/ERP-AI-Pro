using Dewiride.Erp.BuildingBlocks.Idempotency.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Idempotency.Cleanup;

internal sealed partial class IdempotencyCleanupService(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, IOptions<IdempotencyOptions> options, ILogger<IdempotencyCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.CleanupInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await CleanupOnceAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    internal async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var deleted = await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>().DeleteExpiredAsync(timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            LogCleaned(deleted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed {Count} expired idempotency keys.")]
    private partial void LogCleaned(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Removing expired idempotency keys failed; the next run will retry.")]
    private partial void LogFailed(Exception exception);
}
