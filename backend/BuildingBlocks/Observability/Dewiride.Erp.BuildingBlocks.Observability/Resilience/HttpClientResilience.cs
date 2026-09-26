using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using OpenTelemetry.Metrics;

namespace Dewiride.Erp.BuildingBlocks.Observability.Resilience;

// Retrying a POST, PUT, PATCH or DELETE could apply a change twice, so only safe methods are retried; the rest of the
// standard pipeline (rate limiter, total and per-attempt timeouts, circuit breaker) applies to every method.
public static class HttpClientResilience
{
    public const string ResilienceMeter = "Polly";

    public static IHostApplicationBuilder AddErpHttpClientDefaults(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods()));
        builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(ResilienceMeter));

        return builder;
    }
}
