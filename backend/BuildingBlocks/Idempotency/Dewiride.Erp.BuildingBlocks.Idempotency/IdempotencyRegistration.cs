using Dewiride.Erp.BuildingBlocks.Idempotency.Cleanup;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Dewiride.Erp.BuildingBlocks.Idempotency.Persistence;
using Dewiride.Erp.BuildingBlocks.Idempotency.Storage;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.Idempotency;

public static class IdempotencyRegistration
{
    public static IHostApplicationBuilder AddErpIdempotency(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<IdempotencyOptions>()
            .BindConfiguration(IdempotencyOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.AddModuleDbContext<IdempotencyDbContext>(IdempotencyDbContext.SchemaName);
        builder.Services.TryAddScoped<IIdempotencyStore, SqlIdempotencyStore>();
        builder.Services.AddHostedService<IdempotencyCleanupService>();

        return builder;
    }

    public static IApplicationBuilder UseErpIdempotency(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<IdempotencyMiddleware>();
    }
}
