using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;

namespace Dewiride.Erp.BuildingBlocks.Endpoints;

public static class ErpEndpointsExtensions
{
    public const string OpenApiDocumentName = "erp";

    public static IHostApplicationBuilder AddErpEndpoints(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddOpenApi(OpenApiDocumentName, options =>
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "ERP-AI-Pro API";
                document.Info.Description = "Modular ERP for Dewiride. Every route is grouped by domain and module.";
                return Task.CompletedTask;
            }));

        return builder;
    }

    public static WebApplication UseErpEndpointPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        return app;
    }

    public static WebApplication MapErpOpenApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi("/openapi/{documentName}.json");
        app.MapScalarApiReference(options => options.WithTitle("ERP-AI-Pro API").WithOpenApiRoutePattern("/openapi/{documentName}.json").AddDocument(OpenApiDocumentName));

        return app;
    }
}
