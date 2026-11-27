using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Modules.Platform.Approvals.Contracts;
using Dewiride.Erp.Modules.Platform.Approvals.Integration;
using Dewiride.Erp.Modules.Platform.Approvals.Kinds.Endpoints;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Modules.Platform.Approvals;

public sealed class ApprovalsModule : IModule
{
    public const string FeatureFlag = "Erp.Modules.Platform.Approvals";

    public ModuleDescriptor Descriptor { get; } = new(
        Domain: "Platform",
        Name: "Approvals",
        Schema: null,
        RoutePrefix: "/platform/approvals",
        FeatureFlag: FeatureFlag,
        Permissions:
        [
            new(ApprovalsPermissions.Read, [ErpRole.Employee], "See the requests you asked for, the ones waiting for your decision, the ones you decided, and each kind of request with its steps."),
            new(ApprovalsPermissions.Decide, [ErpRole.Employee], "Approve, send back or reject a request that waits for your decision."),
            new(ApprovalsPermissions.Withdraw, [ErpRole.Employee], "Withdraw a request you asked for while it still waits for a decision."),
            new(ApprovalsPermissions.ReadAll, [], "See every approval request, whoever asked for it and whoever decides it."),
            new(ApprovalsPermissions.DecideAny, [], "Decide any waiting request in place of the person or role it waits for, except your own."),
            new(ApprovalsPermissions.ChangeKinds, [], "Switch approval on or off for a kind of request and change how many working days its approvers have."),
        ],
        Capabilities: []);

    public void AddServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHandlersFromAssembly(typeof(ApprovalsModule).Assembly);
        builder.Services.AddScoped<IReportingManagers, UsersReportingManagers>();
        builder.Services.AddValidation();
    }

    public void MapEndpoints(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        RequestEndpoints.Map(group);
        KindEndpoints.Map(group);
    }
}
