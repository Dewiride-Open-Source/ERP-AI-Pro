using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Persistence;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.ParameterSets.Endpoints;

[Collection(StatutoryRows.Name)]
public sealed class ParameterSetRecordNamesTests(ErpApiFactory factory)
{
    [Fact]
    public async Task NamesAsync_SetVersionAndRow_NameEachByItsSetsTitleDateAndKey()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<StatutoryParametersDbContext>();
        var set = await context.ParameterSets.AsNoTracking().SingleAsync(s => s.Key == "gst.sac-codes", TestContext.Current.CancellationToken);
        var version = await context.ParameterVersions.AsNoTracking().Where(v => v.SetId == set.Id).OrderBy(v => v.From).FirstAsync(TestContext.Current.CancellationToken);
        var row = await context.ParameterRows.AsNoTracking().Where(r => r.SetId == set.Id && r.RowKey == "998314").SingleAsync(TestContext.Current.CancellationToken);
        var names = scope.ServiceProvider.GetServices<IRecordNames>().Single(names => names.EntityTypes.Contains(ParameterSetsApi.SetChangeKey));

        var setNames = await names.NamesAsync(ParameterSetsApi.SetChangeKey, [set.Id.Value, Guid.CreateVersion7()], TestContext.Current.CancellationToken);
        var versionNames = await names.NamesAsync(ParameterSetsApi.VersionChangeKey, [version.Id.Value], TestContext.Current.CancellationToken);
        var rowNames = await names.NamesAsync(ParameterSetsApi.RowChangeKey, [row.Id.Value], TestContext.Current.CancellationToken);

        Assert.Equal([ParameterSetsApi.RowChangeKey, ParameterSetsApi.SetChangeKey, ParameterSetsApi.VersionChangeKey], names.EntityTypes.Order(StringComparer.Ordinal));
        Assert.Equal(set.Title, Assert.Single(setNames).Value);
        Assert.Equal($"{set.Title}, version from {version.From:dd-MM-yyyy}", versionNames[version.Id.Value]);
        Assert.Equal($"{set.Title}: 998314 from {row.From:dd-MM-yyyy}", rowNames[row.Id.Value]);
    }

    [Fact]
    public async Task NamesAsync_KeyOfAnotherModule_Throws()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var names = scope.ServiceProvider.GetServices<IRecordNames>().Single(names => names.EntityTypes.Contains(ParameterSetsApi.SetChangeKey));

        await Assert.ThrowsAsync<ArgumentException>(() => names.NamesAsync("platform.reference-data.countries", [Guid.CreateVersion7()], TestContext.Current.CancellationToken));
    }
}
