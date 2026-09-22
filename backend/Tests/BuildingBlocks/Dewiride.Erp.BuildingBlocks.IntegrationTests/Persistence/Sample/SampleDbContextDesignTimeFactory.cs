using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

public sealed class SampleDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SampleDbContext>
{
    public SampleDbContext CreateDbContext(string[] args) => new(Options(DatabaseProvider.SqlServer));

    public static DbContextOptions<SampleDbContext> Options(DatabaseProvider provider, string? connectionString = null)
    {
        var builder = new DbContextOptionsBuilder<SampleDbContext>();
        ModuleDbContextRegistration.Configure(builder, new DatabaseOptions { Provider = provider, ConnectionString = connectionString }, SampleDbContext.SchemaName);

        return builder.Options;
    }
}
