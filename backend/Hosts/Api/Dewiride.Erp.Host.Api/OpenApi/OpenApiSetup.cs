using Dewiride.Erp.BuildingBlocks.Authentication.OpenApi;
using Dewiride.Erp.BuildingBlocks.Endpoints.OpenApi;
using Scalar.AspNetCore;

namespace Dewiride.Erp.Host.Api.OpenApi;

internal static class OpenApiSetup
{
    public const string RoutePattern = "/openapi/{documentName}.json";

    // The document name is a literal because the XML documentation generator only intercepts literal AddOpenApi calls,
    // and this host is the only assembly that references every module, so only here does it see their documentation.
    public static WebApplicationBuilder AddErpOpenApi(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOpenApi("erp", static options =>
        {
            ErpOpenApiOptions.Configure(options);
            options.AddErpSecurityDescription();
        });

        return builder;
    }

    public static WebApplication MapErpOpenApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi(RoutePattern).AllowAnonymous();
        app.MapScalarApiReference(options => options
            .WithTitle(ErpOpenApiOptions.Title)
            .WithOpenApiRoutePattern(RoutePattern)
            .AddDocument(ErpOpenApiOptions.DocumentName))
            .AllowAnonymous();

        return app;
    }
}
