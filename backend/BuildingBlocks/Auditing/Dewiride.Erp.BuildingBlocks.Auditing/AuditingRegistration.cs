using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.Auditing;

public static class AuditingRegistration
{
    public static IHostApplicationBuilder AddErpAuditing(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<AuditingOptions>()
            .BindConfiguration(AuditingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.AddModuleDbContext<AuditingDbContext>(AuditingDbContext.SchemaName);
        builder.Services.TryAddScoped<ISecurityEventRecorder, SqlSecurityEventRecorder>();
        builder.Services.AddHostedService<SecurityEventRetentionSweeper>();

        return builder;
    }
}
