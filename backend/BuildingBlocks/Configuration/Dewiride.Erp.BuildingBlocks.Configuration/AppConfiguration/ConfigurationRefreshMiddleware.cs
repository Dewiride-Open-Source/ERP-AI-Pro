using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Configuration.AppConfiguration;

internal sealed partial class ConfigurationRefreshMiddleware(
    RequestDelegate next,
    IConfigurationRefresherProvider refresherProvider,
    TimeProvider timeProvider,
    ILogger<ConfigurationRefreshMiddleware> logger)
{
    private static readonly long MinimumIntervalTicks = TimeSpan.FromSeconds(1).Ticks;

    private readonly IEnumerable<IConfigurationRefresher> _refreshers = refresherProvider.Refreshers;

    private long _refreshReadyTicks = timeProvider.GetUtcNow().Ticks;

    public Task InvokeAsync(HttpContext context)
    {
        var now = timeProvider.GetUtcNow().Ticks;
        var refreshReadyTicks = Interlocked.Read(ref _refreshReadyTicks);
        if (refreshReadyTicks <= now
            && Interlocked.CompareExchange(ref _refreshReadyTicks, now + MinimumIntervalTicks, refreshReadyTicks) == refreshReadyTicks)
        {
            using (ExecutionContext.SuppressFlow())
            {
                foreach (var refresher in _refreshers)
                {
                    _ = RefreshAsync(refresher);
                }
            }
        }

        return next(context);
    }

    private async Task RefreshAsync(IConfigurationRefresher refresher)
    {
        try
        {
            await refresher.TryRefreshAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogRefreshFaulted(logger, exception, refresher.AppConfigurationEndpoint);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "App Configuration refresh from {Endpoint} faulted")]
    private static partial void LogRefreshFaulted(ILogger logger, Exception exception, Uri endpoint);
}
