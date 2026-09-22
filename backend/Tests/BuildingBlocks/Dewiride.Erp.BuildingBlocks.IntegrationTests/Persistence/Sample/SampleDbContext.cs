using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "test_sample";

    public DbSet<SampleAggregate> Samples => Set<SampleAggregate>();
}
