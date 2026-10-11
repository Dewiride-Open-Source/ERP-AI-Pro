using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests;

internal static class HostStartFailure
{
    private const string HostCategory = "Microsoft.Extensions.Hosting.Internal.Host";

    private const string StartupFaultedEvent = "HostedServiceStartupFaulted";

    // WebApplicationFactory's deferred host looks IHostApplicationLifetime up in the host it waits for, and when the program
    // has already disposed that host after its failed start, the lookup throws ObjectDisposedException in place of the
    // failure. The host logs the failure before it is disposed, so its record carries the exception either way.
    public static Exception StartFailure(this WebApplicationFactory<Program> factory)
    {
        var records = FakeLogCollector.Create(new FakeLogCollectorOptions { FilteredCategories = { HostCategory } });
        using var recording = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<ILoggerProvider>(new FakeLoggerProvider(records))));
        try
        {
            _ = recording.Services;
        }
        catch (Exception thrown)
        {
            return records.GetSnapshot().LastOrDefault(record => record.Id.Name == StartupFaultedEvent)?.Exception ?? thrown;
        }

        throw new InvalidOperationException("The host started, so it has no start failure.");
    }
}
