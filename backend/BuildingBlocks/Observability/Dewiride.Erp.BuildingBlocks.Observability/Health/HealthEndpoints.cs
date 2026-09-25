using System.Net.Mime;
using Dewiride.Erp.BuildingBlocks.Configuration;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.Observability.Health;

public static class HealthEndpoints
{
    public const string ReadyTag = "ready";

    public const string UnhealthyTitle = "A dependency of the API is unavailable.";

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

        endpoints.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteAsync });
        endpoints.MapHealthChecks("/healthz/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag), ResponseWriter = WriteAsync });

        return endpoints;
    }

    // An unhealthy report is an error response like any other; it names no check, because the endpoint is anonymous.
    private static Task WriteAsync(HttpContext context, HealthReport report)
    {
        if (report.Status != HealthStatus.Unhealthy)
        {
            context.Response.ContentType = MediaTypeNames.Text.Plain;
            return context.Response.WriteAsync(report.Status.ToString(), context.RequestAborted);
        }

        return context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = StatusCodes.Status503ServiceUnavailable, Title = UnhealthyTitle },
        }).AsTask();
    }
}
