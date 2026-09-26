using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Modules.Platform.Attachments.Contracts.Files;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints;
using Microsoft.AspNetCore.Http.Timeouts;
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
        builder.Services.AddValidation();
    }

    public void MapEndpoints(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        AttachmentEndpoints.Map(group);
        TransferEndpoints.Map(group);
    }
}
