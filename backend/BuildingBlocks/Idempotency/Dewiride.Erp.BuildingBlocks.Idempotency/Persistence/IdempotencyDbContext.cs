using Dewiride.Erp.BuildingBlocks.Idempotency.Storage;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Idempotency.Persistence;

internal sealed class IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "platform_idempotency";

    public DbSet<IdempotencyRecord> Records => Set<IdempotencyRecord>();
}
