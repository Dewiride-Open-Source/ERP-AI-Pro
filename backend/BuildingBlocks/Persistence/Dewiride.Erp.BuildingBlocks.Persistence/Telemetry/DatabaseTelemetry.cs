using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Telemetry;

// SqlClient 7 and EF Core 10 start no activities of their own, so SQL spans and db.client.operation.duration come from the
// OpenTelemetry SqlClient instrumentation. It records the parameterised query text and no parameter values unless
// OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_DB_QUERY_PARAMETERS is set, which docs/guides/observability.md forbids.
public static class DatabaseTelemetry
{
    public const string EntityFrameworkCoreMeter = "Microsoft.EntityFrameworkCore";

    public static IServiceCollection AddErpDatabaseTelemetry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSqlClientInstrumentation());
        services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(EntityFrameworkCoreMeter).AddSqlClientInstrumentation());

        return services;
    }
}
