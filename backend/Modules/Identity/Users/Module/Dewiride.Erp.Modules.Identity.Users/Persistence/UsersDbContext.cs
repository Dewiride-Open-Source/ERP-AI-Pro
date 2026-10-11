using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Positions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Persistence;

internal sealed class UsersDbContext(DbContextOptions<UsersDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "identity_users";

    public DbSet<User> Users => Set<User>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Designation> Designations => Set<Designation>();

    public DbSet<Position> Positions => Set<Position>();
}
