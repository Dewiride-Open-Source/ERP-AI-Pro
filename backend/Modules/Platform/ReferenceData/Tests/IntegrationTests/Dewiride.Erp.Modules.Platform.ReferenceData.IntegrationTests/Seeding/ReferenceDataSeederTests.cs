using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;
using Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Persistence;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Persistence;
using Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.ExchangeRates.Endpoints;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.Seeding;

// The test database ran every seeder when it was made, so running one again is its second run.
[Collection(ReferenceRowReaders.Name)]
public sealed class ReferenceDataSeederTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task CountrySeeder_RunAgain_LeavesTheSameRowsAndRecordsNoChange()
    {
        var before = await CountriesAsync();
        var changes = await ChangeCountAsync("platform.reference-data.countries");

        await SeedAsync<CountrySeeder>();

        Assert.Equal(250, before.Count);
        Assert.Equal(before, await CountriesAsync());
        Assert.Equal(changes, await ChangeCountAsync("platform.reference-data.countries"));
    }

    [Fact]
    public async Task IndianStateSeeder_RunAgain_LeavesTheSameRowsAndRecordsNoChange()
    {
        var before = await StatesAsync();
        var changes = await ChangeCountAsync("platform.reference-data.indian-states");

        await SeedAsync<IndianStateSeeder>();

        Assert.Equal(40, before.Count);
        Assert.Equal(before, await StatesAsync());
        Assert.Equal(changes, await ChangeCountAsync("platform.reference-data.indian-states"));
    }

    [Fact]
    public async Task CountrySeeder_NameThatDiffersFromTheFile_TakesTheFilesNameBack()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ReferenceDataDbContext>();
            var antarctica = await context.Countries.SingleAsync(c => c.Code == "AQ", TestContext.Current.CancellationToken);
            antarctica.Rename("Antarctica before the file");
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await SeedAsync<CountrySeeder>();

        var seededRow = Assert.Single(await CountriesAsync(), row => row.Code == "AQ");
        Assert.Equal("Antarctica", seededRow.Name);
        using var administrator = factory.CreateClient().AsUser(TestUsers.Administrator);
        var seeded = (await ExchangeRatesApi.ChangesOfAsync(administrator, seededRow.Id))[^1];
        Assert.Equal(ChangeOperation.Updated, seeded.Operation);
        Assert.Equal("platform.reference-data.countries", seeded.EntityType);
        Assert.Equal("Antarctica", seeded.SubjectName);
        Assert.Equal(ActorIds.System, seeded.ActorId);
        Assert.Contains(seeded.Changes, value => value is { Property: "name", Before: "Antarctica before the file", After: "Antarctica" });
    }

    [Fact]
    public async Task IndianStateSeeder_RowThatDiffersFromTheFile_TakesTheFilesValuesBack()
    {
        var expected = Assert.Single(IndianStateSourceFile.Read().States, entry => entry.GstCode == "25");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ReferenceDataDbContext>();
            var row = await context.IndianStates.SingleAsync(s => s.GstCode == "25", TestContext.Current.CancellationToken);
            row.Revise("Daman and Diu before the file", TerritoryKind.State, null, "No instrument");
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await SeedAsync<IndianStateSeeder>();

        await using var check = factory.Services.CreateAsyncScope();
        var stored = await check.ServiceProvider.GetRequiredService<ReferenceDataDbContext>().IndianStates
            .AsNoTracking()
            .SingleAsync(s => s.GstCode == "25", TestContext.Current.CancellationToken);
        Assert.Equal((expected.Name, expected.Kind, expected.ValidFrom, expected.ValidUntil, expected.Source), (stored.Name, stored.Kind, stored.ValidFrom, stored.ValidUntil, stored.Source));
    }

    [Fact]
    public async Task CountrySeeder_CodeTheFileDoesNotList_DeletesItsRow()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ReferenceDataDbContext>();
            context.Countries.Add(Country.Create("ZZ", "No such country"));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await SeedAsync<CountrySeeder>();

        var countries = await CountriesAsync();
        Assert.DoesNotContain(countries, row => row.Code == "ZZ");
        Assert.Equal(250, countries.Count);
    }

    [Fact]
    public async Task IndianStateSeeder_RowTheFileDoesNotList_DeletesIt()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ReferenceDataDbContext>();
            context.IndianStates.Add(IndianState.Create("38", new DateOnly(2019, 10, 31), "Ladakh", TerritoryKind.UnionTerritory, null, "A start the file corrected"));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await SeedAsync<IndianStateSeeder>();

        var ladakh = (await StatesAsync()).Where(row => row.Code == "38").ToList();
        Assert.Equal(new DateOnly(2020, 1, 1), Assert.Single(ladakh).From);
    }

    private async Task SeedAsync<TSeeder>()
        where TSeeder : ISeeder
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TSeeder>().SeedAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<(Guid Id, string Code, string Name, DateTimeOffset CreatedAt)>> CountriesAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<ReferenceDataDbContext>().Countries
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new { c.Id, c.Code, c.Name, c.CreatedAt })
            .ToListAsync(TestContext.Current.CancellationToken);

        return [.. rows.Select(row => (row.Id.Value, row.Code, row.Name, row.CreatedAt))];
    }

    private async Task<List<(Guid Id, string Code, DateOnly From, string Name, DateOnly? Until, DateTimeOffset? ModifiedAt)>> StatesAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<ReferenceDataDbContext>().IndianStates
            .AsNoTracking()
            .OrderBy(s => s.GstCode)
            .ThenBy(s => s.ValidFrom)
            .Select(s => new { s.Id, s.GstCode, s.ValidFrom, s.Name, s.ValidUntil, s.ModifiedAt })
            .ToListAsync(TestContext.Current.CancellationToken);

        return [.. rows.Select(row => (row.Id.Value, row.GstCode, row.ValidFrom, row.Name, row.ValidUntil, row.ModifiedAt))];
    }

    private async Task<long> ChangeCountAsync(string changeKey)
    {
        using var administrator = factory.CreateClient().AsUser(TestUsers.Administrator);

        return await ExchangeRatesApi.ChangeCountAsync(administrator, changeKey);
    }
}
