using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Persistence.Catalog;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.Testing.Sql;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence;

public sealed class PendingModelChangesTests
{
    [Theory]
    [InlineData(DatabaseProvider.SqlServer)]
    [InlineData(DatabaseProvider.AzureSql)]
    public void HasPendingModelChanges_EveryCatalogueContext_IsFalse(DatabaseProvider provider)
    {
        using var host = TestDatabaseHost.Build(SqlTestDatabase.Current.ConnectionString, provider);
        using var scope = host.Services.CreateScope();
        var registrations = host.Services.GetRequiredService<DbContextCatalog>().Registrations;

        var pending = registrations
            .Select(r => (DbContext)scope.ServiceProvider.GetRequiredService(r.ContextType))
            .Where(context => context.Database.HasPendingModelChanges())
            .Select(context => context.GetType().Name)
            .ToList();

        Assert.NotEmpty(registrations);
        Assert.Empty(pending);
    }

    [Theory]
    [InlineData(DatabaseProvider.SqlServer)]
    [InlineData(DatabaseProvider.AzureSql)]
    public void HasPendingModelChanges_SampleContext_IsFalse(DatabaseProvider provider)
    {
        using var context = new SampleDbContext(SampleDbContextDesignTimeFactory.Options(provider));

        Assert.False(context.Database.HasPendingModelChanges());
    }
}
