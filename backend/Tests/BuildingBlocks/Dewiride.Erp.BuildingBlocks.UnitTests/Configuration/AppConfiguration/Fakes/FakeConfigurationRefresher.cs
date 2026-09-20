using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.AppConfiguration.Fakes;

internal sealed class FakeConfigurationRefresher : IConfigurationRefresher
{
    private int _calls;

    public Uri AppConfigurationEndpoint { get; } = new("https://example.azconfig.io");

    public int Calls => Volatile.Read(ref _calls);

    public Exception? Fault { get; init; }

    public Task RefreshAsync(CancellationToken cancellationToken = default) => TryRefreshAsync(cancellationToken);

    public Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);

        return Fault is null ? Task.FromResult(true) : Task.FromException<bool>(Fault);
    }

    public void ProcessPushNotification(PushNotification pushNotification, TimeSpan? maxDelay = null)
    {
    }
}
