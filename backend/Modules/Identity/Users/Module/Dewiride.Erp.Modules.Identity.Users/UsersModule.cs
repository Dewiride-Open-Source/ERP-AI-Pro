using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Contracts;
using Dewiride.Erp.Modules.Identity.Users.Departments.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.Designations.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.Integration;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints;
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
        Permissions:
        [
            new(UsersPermissions.Read, []),
            new(UsersPermissions.Register, []),
            new(UsersPermissions.Update, []),
            new(UsersPermissions.ChangeStatus, []),
            new(UsersPermissions.Delete, []),
            new(UsersPermissions.AssignRoles, []),
            new(UsersPermissions.ReadDepartments, [ErpRole.Employee]),
            new(UsersPermissions.CreateDepartments, []),
            new(UsersPermissions.UpdateDepartments, []),
            new(UsersPermissions.DeleteDepartments, []),
            new(UsersPermissions.ReadDesignations, [ErpRole.Employee]),
            new(UsersPermissions.CreateDesignations, []),
            new(UsersPermissions.UpdateDesignations, []),
            new(UsersPermissions.DeleteDesignations, []),
            new(UsersPermissions.ReadPositions, []),
            new(UsersPermissions.RecordPositions, []),
            new(UsersPermissions.WithdrawPositions, []),
            new(UsersPermissions.ReadOrgChart, [ErpRole.Employee]),
        ],
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
        DepartmentEndpoints.Map(group);
        DesignationEndpoints.Map(group);
        PositionEndpoints.Map(group);
        OrgChartEndpoints.Map(group);
    }
}
