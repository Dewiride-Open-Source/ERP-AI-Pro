using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.Numbering.Rules;
using Dewiride.Erp.Modules.Platform.Numbering.Contracts;
using Dewiride.Erp.Modules.Platform.Numbering.Integration;
using Dewiride.Erp.Modules.Platform.Numbering.Series.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Modules.Platform.Numbering;

public sealed class NumberingModule : IModule
{
    public const string FeatureFlag = "Erp.Modules.Platform.Numbering";

    public ModuleDescriptor Descriptor { get; } = new(
        Domain: "Platform",
        Name: "Numbering",
        Schema: null,
        RoutePrefix: "/platform/numbering",
        FeatureFlag: FeatureFlag,
        Permissions:
        [
            new(NumberingPermissions.Read, [ErpRole.Finance], "See how invoices and other documents are numbered and which numbers each financial year has used."),
            new(NumberingPermissions.Change, [ErpRole.Finance], "Set up and change how invoices and other documents are numbered."),
        ],
        Capabilities: []);

    public void AddServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHandlersFromAssembly(typeof(NumberingModule).Assembly);
        builder.Services.AddScoped<ISerialNumberRules, GstSerialNumberRules>();
        builder.Services.AddValidation();
    }

    public void MapEndpoints(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        SeriesEndpoints.Map(group);
    }
}
