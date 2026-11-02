using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Modules.Platform.Jobs.Contracts;
using Dewiride.Erp.Modules.Platform.Jobs.Runs.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Modules.Platform.Jobs;

public sealed class JobsModule : IModule
{
    public const string FeatureFlag = "Erp.Modules.Platform.Jobs";

    public ModuleDescriptor Descriptor { get; } = new(
        Domain: "Platform",
        Name: "Jobs",
        Schema: null,
        RoutePrefix: "/platform/jobs",
        FeatureFlag: FeatureFlag,
        Permissions:
        [
            new(JobsPermissions.Read, [], "See the jobs the ERP runs on its own, when each runs next, and every run with its attempts and errors."),
            new(JobsPermissions.Rerun, [], "Run again a job run that failed, gave up or was cancelled."),
            new(JobsPermissions.Cancel, [], "Cancel a job run that has not finished, or ask a running one to stop."),
        ],
        Capabilities: []);

    public void AddServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHandlersFromAssembly(typeof(JobsModule).Assembly);
        builder.Services.AddValidation();
    }

    public void MapEndpoints(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        RunEndpoints.Map(group);
    }
}
