using Dewiride.Erp.BuildingBlocks.Configuration;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.Observability.Health;

public static class HealthEndpoints
{
    public const string ReadyTag = "ready";

    public static IHostApplicationBuilder AddErpHealthChecks(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var checks = builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), tags: [ReadyTag]);

        if (builder.GetErpConfigurationInfo().Source == ErpConfigurationSource.AppConfiguration)
        {
            checks.AddAzureAppConfiguration(name: "app-configuration", tags: [ReadyTag]);
        }

        return builder;
    }

    public static IEndpointRouteBuilder MapErpHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks("/healthz/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) });

        return endpoints;
    }
}
