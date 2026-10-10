using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Persistence;

internal sealed class UsersDbContext(DbContextOptions<UsersDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "identity_users";

    public DbSet<User> Users => Set<User>();
}
