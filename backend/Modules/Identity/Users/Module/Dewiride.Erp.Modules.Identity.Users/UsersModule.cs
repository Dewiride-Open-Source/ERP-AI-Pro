using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Identity.Users.Contracts;
using Dewiride.Erp.Modules.Identity.Users.Departments.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.Designations.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.Integration;
using Dewiride.Erp.Modules.Identity.Users.People.Application;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.Permissions.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.Profile.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
            new(UsersPermissions.Read, [], "See the people registered in the ERP, the details of each one and what each role may do."),
            new(UsersPermissions.Register, [], "Register a person before their first sign-in, by work email or from the company directory."),
            new(UsersPermissions.Update, [], "Change a person's employee code, phone number and date of joining."),
            new(UsersPermissions.ChangeStatus, [], "Deactivate a person, which signs them out and stops them signing in, or reactivate them."),
            new(UsersPermissions.Delete, [], "Delete a person's record, which ends their open sessions."),
            new(UsersPermissions.AssignRoles, [], "Give people the Finance, HR and Manager roles or take them away, also when registering or inviting them."),
            new(UsersPermissions.ReadDepartments, [ErpRole.Employee], "See the departments and which department each one sits under."),
            new(UsersPermissions.CreateDepartments, [], "Add a department, at the top or under another department."),
            new(UsersPermissions.UpdateDepartments, [], "Rename a department or move it under another department."),
            new(UsersPermissions.DeleteDepartments, [], "Delete a department that holds no other department and that nobody is or will be in."),
            new(UsersPermissions.ReadDesignations, [ErpRole.Employee], "See the designations people can hold."),
            new(UsersPermissions.CreateDesignations, [], "Add a designation."),
            new(UsersPermissions.UpdateDesignations, [], "Rename a designation."),
            new(UsersPermissions.DeleteDesignations, [], "Delete a designation that nobody holds or will hold."),
            new(UsersPermissions.ReadPositions, [], "See each person's past and planned moves, and the organisation chart of any date."),
            new(UsersPermissions.RecordPositions, [], "Record a person's move to a department, designation or manager from a date."),
            new(UsersPermissions.WithdrawPositions, [], "Withdraw a move recorded for a person."),
            new(UsersPermissions.ReadOrgChart, [ErpRole.Employee], "See today's organisation chart: who is in each department and who reports to whom."),
        ],
        Capabilities: []);

    public void AddServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<UsersDbContext>(UsersDbContext.SchemaName);
        builder.Services.AddHandlersFromAssembly(typeof(UsersModule).Assembly);
        builder.Services.AddScoped<DirectoryAccountCheck>();
        builder.Services.AddScoped<IPersonAdmission, UserAdmission>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecordNames, UserRecordNames>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IPersonNames, UserRecordNames>());
        builder.Services.AddValidation();
    }

    public void MapEndpoints(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        PeopleEndpoints.Map(group);
        DepartmentEndpoints.Map(group);
        DesignationEndpoints.Map(group);
        PositionEndpoints.Map(group);
        TeamEndpoints.Map(group);
        OrgChartEndpoints.Map(group);
        PermissionEndpoints.Map(group);
        ProfileEndpoints.Map(group);
    }
}
