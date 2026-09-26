using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

public sealed class SampleDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SampleDbContext>
{
    public SampleDbContext CreateDbContext(string[] args) => new(Options(DatabaseProvider.SqlServer));

    public static DbContextOptions<SampleDbContext> Options(DatabaseProvider provider, string? connectionString = null)
    {
        var builder = new DbContextOptionsBuilder<SampleDbContext>();
        ModuleDbContextRegistration.Configure(builder, new DatabaseOptions { Provider = provider, ConnectionString = connectionString }, SampleDbContext.SchemaName);

        // Every test host and fixture in the process builds its own EF Core internal service provider, which EF Core reports
        // as an error past twenty unless the warning is only logged, as it is for the API test hosts.
        builder.ConfigureWarnings(warnings => warnings.Log(CoreEventId.ManyServiceProvidersCreatedWarning));

        return builder.Options;
    }
}
