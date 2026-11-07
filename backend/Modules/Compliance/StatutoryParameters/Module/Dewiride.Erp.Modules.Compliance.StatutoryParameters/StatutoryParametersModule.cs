using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Integration;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Persistence;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.PublicApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters;

// The seeder runs after the reference data's, whose GST State codes it checks every State code cell against.
public sealed class StatutoryParametersModule : IModule
{
    public const string FeatureFlag = "Erp.Modules.Compliance.StatutoryParameters";

    public ModuleDescriptor Descriptor { get; } = new(
        Domain: "Compliance",
        Name: "StatutoryParameters",
        Schema: StatutoryParametersDbContext.SchemaName,
        RoutePrefix: "/compliance/statutory-parameters",
        FeatureFlag: FeatureFlag,
        Permissions:
        [
            new(StatutoryParametersPermissions.ReadParameterSets, [ErpRole.Finance], "See the government rates, codes and due dates the ERP uses, and their history."),
        ],
        Capabilities: []);

    public void AddServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<StatutoryParametersDbContext>(StatutoryParametersDbContext.SchemaName);
        builder.Services.AddHandlersFromAssembly(typeof(StatutoryParametersModule).Assembly);
        builder.Services.AddScoped<IStatutoryParameters, StatutoryParameterStore>();
        builder.Services.AddScoped<IStatutoryParameterHistory, StatutoryParameterHistory>();
        builder.Services.AddSeeder<StatutoryParameterSeeder>(200);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecordNames, ParameterSetRecordNames>());
        builder.Services.AddValidation();
    }

    public void MapEndpoints(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        ParameterSetEndpoints.Map(group);
    }
}
