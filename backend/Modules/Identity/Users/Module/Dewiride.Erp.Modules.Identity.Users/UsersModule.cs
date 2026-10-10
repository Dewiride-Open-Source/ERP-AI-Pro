using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.Integration;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Modules.Identity.Users;

public sealed class UsersModule : IModule
{
    public const string FeatureFlag = "Erp.Modules.Identity.Users";

    public ModuleDescriptor Descriptor { get; } = new(
        Domain: "Identity",
        Name: "Users",
        Schema: UsersDbContext.SchemaName,
        RoutePrefix: "/identity/users",
        FeatureFlag: FeatureFlag,
        Permissions: UsersPermissions.All,
        Capabilities: []);

    public void AddServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<UsersDbContext>(UsersDbContext.SchemaName);
        builder.Services.AddHandlersFromAssembly(typeof(UsersModule).Assembly);
        builder.Services.AddScoped<IPersonAdmission, UserAdmission>();
        builder.Services.AddValidation();
    }

    public void MapEndpoints(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        PeopleEndpoints.Map(group);
    }
}
