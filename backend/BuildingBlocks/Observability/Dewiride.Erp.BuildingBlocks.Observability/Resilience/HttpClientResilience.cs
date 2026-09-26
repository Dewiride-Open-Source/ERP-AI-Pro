using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using OpenTelemetry.Metrics;

namespace Dewiride.Erp.BuildingBlocks.Observability.Resilience;

// Retrying a POST, PUT, PATCH or DELETE could apply a change twice, so only safe methods are retried; the rest of the
// standard pipeline (rate limiter, total and per-attempt timeouts, circuit breaker) applies to every method. The defaults
// register one pipeline for every client, so it is selected per scheme, host and port: a failing dependency opens only
// its own circuit and spends only its own concurrency permits.
public static class HttpClientResilience
{
    public const string ResilienceMeter = "Polly";

    public static IHostApplicationBuilder AddErpHttpClientDefaults(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods()).SelectPipelineByAuthority());
        builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(ResilienceMeter));

        return builder;
    }
}
