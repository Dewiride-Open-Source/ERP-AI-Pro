using System.Threading.RateLimiting;
using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints;
using Dewiride.Erp.Modules.Platform.Audit.Contracts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Modules.Platform.Audit;

public sealed class AuditModule : IModule
{
    public const string FeatureFlag = "Erp.Modules.Platform.Audit";

    public ModuleDescriptor Descriptor { get; } = new(
        Domain: "Platform",
        Name: "Audit",
        Schema: null,
        RoutePrefix: "/platform/audit",
        FeatureFlag: FeatureFlag,
        Permissions:
        [
            new(AuditPermissions.ReadChanges, []),
            new(AuditPermissions.ExportChanges, []),
        ],
        Capabilities: []);

    public void AddServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHandlersFromAssembly(typeof(AuditModule).Assembly);

        // One partition for the whole process: an export in flight reads the history a page at a time for up to five minutes, so
        // the cap bounds that load on the database, whoever asks.
        builder.Services.AddOptions<RateLimiterOptions>().Configure(limiter =>
            limiter.AddPolicy(ChangeEndpoints.ExportConcurrencyPolicy, _ => RateLimitPartition.GetConcurrencyLimiter(
                ChangeEndpoints.ExportConcurrencyPolicy,
                _ => new ConcurrencyLimiterOptions { PermitLimit = ChangeEndpoints.MaxConcurrentExports, QueueLimit = 0 })));
        builder.Services.AddValidation();
    }

    public void MapEndpoints(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        ChangeEndpoints.Map(group);
    }
}
