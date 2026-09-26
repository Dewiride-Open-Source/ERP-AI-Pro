using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Dewiride.Erp.BuildingBlocks.Observability.Telemetry;

public static class OpenTelemetrySetup
{
    public const string ActivitySourcePrefix = "Dewiride.Erp";

    public const string OtlpEndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public static IHostApplicationBuilder AddErpTelemetry(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var serviceName = builder.Configuration[$"{ErpHostOptions.SectionName}:ApplicationName"] ?? new ErpHostOptions().ApplicationName;

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddSingleton<ErpResourceDetector>();
        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, autoGenerateServiceInstanceId: false, serviceInstanceId: Environment.MachineName)
                .AddDetector(provider => provider.GetRequiredService<ErpResourceDetector>()))
            .WithMetrics(metrics => metrics.AddMeter([.. TelemetryMeters.Names]))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !context.Request.Path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase))
                .AddHttpClientInstrumentation()
                .AddSource($"{ActivitySourcePrefix}.*"));

        if (!string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointVariable]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }
}
