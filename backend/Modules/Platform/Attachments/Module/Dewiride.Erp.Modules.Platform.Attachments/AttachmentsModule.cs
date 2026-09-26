using System.Threading.RateLimiting;
using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Modules.Platform.Attachments.Contracts.Files;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Modules.Platform.Attachments;

public sealed class AttachmentsModule : IModule
{
    public const string FeatureFlag = "Erp.Modules.Platform.Attachments";

    public ModuleDescriptor Descriptor { get; } = new(
        Domain: "Platform",
        Name: "Attachments",
        Schema: null,
        RoutePrefix: "/platform/attachments",
        FeatureFlag: FeatureFlag,
        Permissions: AttachmentsPermissions.All,
        Capabilities: []);

    public void AddServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHandlersFromAssembly(typeof(AttachmentsModule).Assembly);
        builder.Services.AddOptions<RequestTimeoutOptions>().Configure<IOptions<AttachmentsOptions>>((timeouts, attachments) =>
            timeouts.AddPolicy(TransferEndpoints.TimeoutPolicy, new RequestTimeoutPolicy { Timeout = attachments.Value.TransferTimeout }));

        // One partition for the whole process: the cap bounds the memory that uploads in flight hold, whoever sends them.
        builder.Services.AddOptions<RateLimiterOptions>().Configure<IOptions<AttachmentsOptions>>((limiter, attachments) =>
            limiter.AddPolicy(TransferEndpoints.UploadConcurrencyPolicy, _ => RateLimitPartition.GetConcurrencyLimiter(
                TransferEndpoints.UploadConcurrencyPolicy,
                _ => new ConcurrencyLimiterOptions { PermitLimit = attachments.Value.MaxConcurrentUploads, QueueLimit = 0 })));
        builder.Services.AddValidation();
    }

    public void MapEndpoints(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        AttachmentEndpoints.Map(group);
        TransferEndpoints.Map(group);
    }
}
