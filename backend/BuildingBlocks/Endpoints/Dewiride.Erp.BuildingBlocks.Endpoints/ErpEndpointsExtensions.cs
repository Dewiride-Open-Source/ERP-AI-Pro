using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Actors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;
using Dewiride.Erp.BuildingBlocks.Endpoints.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Endpoints;

public static class ErpEndpointsExtensions
{
    public static IHostApplicationBuilder AddErpEndpoints(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsCustomizer.Customize);
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        // Outside Development the binder would answer a value it cannot read with a bare 400; throwing routes every binding failure through GlobalExceptionHandler.
        builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        // Numbers are JSON numbers on the wire in both directions and enums are camelCase names, so the document describes one
        // type per member and the generated client carries the meaning of an enum value rather than its number.
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        });
        builder.Services.AddRequestTimeouts();
        builder.Services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddRateLimiter(static _ => { });
        builder.Services.AddSingleton<IConfigureOptions<RateLimiterOptions>, RateLimiterOptionsSetup>();
        builder.Services.AddOptions<AllowedHostsOptions>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.Replace(ServiceDescriptor.Scoped<IActorContext, HttpActorContext>());

        return builder;
    }

    public static WebApplication UseErpEndpointPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseMiddleware<AllowedHostsMiddleware>();
        app.UseRouting();
        app.UseRequestTimeouts();
        app.UseMiddleware<DatabaseCancellationMiddleware>();

        return app;
    }

    public static IApplicationBuilder UseErpRateLimiting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseRateLimiter();
    }
}
