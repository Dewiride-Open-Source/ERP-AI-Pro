using System.Text;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.ParameterSets.Endpoints;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Persistence;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;

// The test database ran every seeder when it was made, so running one again is its second run. A test that changes a seeded
// row runs the seeder before it ends, so the next test reads the files' rows.
[Collection(StatutoryRows.Name)]
public sealed class StatutoryParameterSeederTests(ErpApiFactory factory)
{
    private const string CentralTaxRates = "gst.central-tax-rates";

    private static readonly string[] ChangeKeys = [ParameterSetsApi.SetChangeKey, ParameterSetsApi.VersionChangeKey, ParameterSetsApi.RowChangeKey];

    [Fact]
    public async Task Seeder_RunAgain_LeavesTheSameSetsVersionsAndRowsAndRecordsNoChange()
    {
        var before = await TablesAsync();
        var changes = await ChangeCountsAsync();

        await SeedAsync();

        Assert.Equal(8, before.Count(line => line.StartsWith("set ", StringComparison.Ordinal)));
        Assert.Equal(before, await TablesAsync());
        Assert.Equal(changes, await ChangeCountsAsync());
    }

    [Fact]
    public async Task Seeder_RowThatDiffersFromTheFile_TakesTheFilesCellsAndSourceBack()
    {
        Guid rowId;
        string expectedCells;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<StatutoryParametersDbContext>();
            var set = await context.ParameterSets.SingleAsync(s => s.Key == CentralTaxRates, TestContext.Current.CancellationToken);
            var row = await context.ParameterRows.Where(r => r.SetId == set.Id && r.RowKey == "21(i)").SingleAsync(TestContext.Current.CancellationToken);
            rowId = row.Id.Value;
            expectedCells = $"\"rate\":\"{row.Cells["rate"]}\"";
            row.Revise(set, row.Until, ParameterCells.Of(row.Cells.Values.Select(cell => cell.Key == "rate" ? KeyValuePair.Create(cell.Key, (string?)"3") : cell)), "A source the file does not hold");
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await SeedAsync();

        var setId = await SetIdAsync(CentralTaxRates);
        var seeded = (await ChangesOfAsync(setId)).Last(change => change.EntityId == rowId);
        Assert.Equal((ChangeOperation.Updated, ParameterSetsApi.RowChangeKey, ParameterSetsApi.SetChangeKey, ActorIds.System), (seeded.Operation, seeded.EntityType, seeded.SubjectType, seeded.ActorId));
        Assert.Contains(seeded.Changes, value => value is { Property: "cells", Kind: ChangeValueKind.Text } && value.Before!.Contains("\"rate\":\"3\"", StringComparison.Ordinal) && value.After!.Contains(expectedCells, StringComparison.Ordinal));
        Assert.Contains(seeded.Changes, value => value is { Property: "source", Before: "A source the file does not hold" });
    }

    [Fact]
    public async Task Seeder_VersionDeletedFromTheTable_AddsItBack()
    {
        DateOnly from;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<StatutoryParametersDbContext>();
            var set = await context.ParameterSets.SingleAsync(s => s.Key == CentralTaxRates, TestContext.Current.CancellationToken);
            var version = await context.ParameterVersions.Where(v => v.SetId == set.Id).OrderBy(v => v.From).Skip(1).FirstAsync(TestContext.Current.CancellationToken);
            from = version.From;
            context.ParameterVersions.Remove(version);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await SeedAsync();

        var setId = await SetIdAsync(CentralTaxRates);
        await using var check = factory.Services.CreateAsyncScope();
        var added = await check.ServiceProvider.GetRequiredService<StatutoryParametersDbContext>().ParameterVersions
            .AsNoTracking()
            .SingleAsync(v => v.SetId == ParameterSetId.From(setId) && v.From == from, TestContext.Current.CancellationToken);
        var created = (await ChangesOfAsync(setId)).Last(change => change.EntityId == added.Id.Value);
        Assert.Equal((ChangeOperation.Created, ParameterSetsApi.VersionChangeKey, ParameterSetsApi.SetChangeKey, ActorIds.System), (created.Operation, created.EntityType, created.SubjectType, created.ActorId));
        Assert.Contains(created.Changes, value => value is { Property: "instruments", Kind: ChangeValueKind.List });
    }

    [Fact]
    public async Task Seeder_RowTheFileDoesNotHold_DeletesIt()
    {
        Guid strayId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<StatutoryParametersDbContext>();
            var set = await context.ParameterSets.SingleAsync(s => s.Key == CentralTaxRates, TestContext.Current.CancellationToken);
            var from = await FirstVersionAsync(context, set);
            var stray = ParameterRow.Create(set, from, null, ParameterCells.Of([
                KeyValuePair.Create("entry", (string?)"99(z)"),
                KeyValuePair.Create("heading", (string?)"9999"),
                KeyValuePair.Create("description", (string?)"A row the file does not hold."),
                KeyValuePair.Create("rate", (string?)"1"),
                KeyValuePair.Create("condition", (string?)null)]), "No instrument");
            strayId = stray.Id.Value;
            context.ParameterRows.Add(stray);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await SeedAsync();

        var setId = await SetIdAsync(CentralTaxRates);
        await using var check = factory.Services.CreateAsyncScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<StatutoryParametersDbContext>().ParameterRows
            .AnyAsync(r => r.Id == ParameterRowId.From(strayId), TestContext.Current.CancellationToken));
        var deleted = (await ChangesOfAsync(setId)).Last(change => change.EntityId == strayId);
        Assert.Equal((ChangeOperation.Deleted, ParameterSetsApi.RowChangeKey, ActorIds.System), (deleted.Operation, deleted.EntityType, deleted.ActorId));
    }

    [Fact]
    public async Task Seeder_SetNoFileHolds_DeletesItWithItsVersionsAndRows()
    {
        Guid strayId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<StatutoryParametersDbContext>();
            var columns = ParameterColumnSet.Create([new ParameterColumn("code", "Code", CellKind.Code, true, false, false, null, null, [])]);
            var stray = ParameterSet.Create("test.stray-set", "A stray set", "A set no file holds.", SetBasis.Law, AsOfDate.SupplyDate, columns);
            strayId = stray.Id.Value;
            context.ParameterSets.Add(stray);
            context.ParameterVersions.Add(ParameterVersion.Create(stray, new DateOnly(2001, 1, 1), ["No instrument"]));
            context.ParameterRows.Add(ParameterRow.Create(stray, new DateOnly(2001, 1, 1), null, ParameterCells.Of([KeyValuePair.Create("code", (string?)"a")]), "No instrument"));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await SeedAsync();

        await using var check = factory.Services.CreateAsyncScope();
        var stored = check.ServiceProvider.GetRequiredService<StatutoryParametersDbContext>();
        var id = ParameterSetId.From(strayId);
        Assert.False(await stored.ParameterSets.AnyAsync(s => s.Id == id, TestContext.Current.CancellationToken));
        Assert.False(await stored.ParameterVersions.AnyAsync(v => v.SetId == id, TestContext.Current.CancellationToken));
        Assert.False(await stored.ParameterRows.AnyAsync(r => r.SetId == id, TestContext.Current.CancellationToken));
        Assert.Equal(
            [ChangeOperation.Deleted, ChangeOperation.Deleted, ChangeOperation.Deleted],
            (await ChangesOfAsync(strayId)).Where(change => change.ActorId == ActorIds.System && change.Operation == ChangeOperation.Deleted).Select(change => change.Operation));
    }

    [Fact]
    public async Task Seeder_ValidFileBesideADefectiveOne_WritesNothing()
    {
        var before = await TablesAsync();
        var changes = await ChangeCountsAsync();
        var valid = Probe("test.probe-valid", "9");
        var defective = Probe("test.probe-defective", "\"9\"");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<StatutoryParameterSeeder>();
            var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                seeder.SeedAsync([.. StatutorySeedFiles.Embedded(), valid, defective], TestContext.Current.CancellationToken));
            Assert.Contains("test.probe-defective.json", refusal.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("test.probe-valid.json", refusal.Message, StringComparison.Ordinal);
        }

        await using var check = factory.Services.CreateAsyncScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<StatutoryParametersDbContext>().ParameterSets
            .AnyAsync(s => s.Key == "test.probe-valid", TestContext.Current.CancellationToken));
        Assert.Equal(before, await TablesAsync());
        Assert.Equal(changes, await ChangeCountsAsync());
    }

    private static StatutorySeedSource Probe(string key, string rate) =>
        new($"{key}.json", Encoding.UTF8.GetBytes($$"""
            {
              "key": "{{key}}",
              "title": "Probe set",
              "description": "A set the tests write.",
              "basis": "law",
              "asOf": "supplyDate",
              "columns": [
                { "name": "entry", "title": "Entry", "kind": "code", "key": true },
                { "name": "rate", "title": "Rate", "kind": "percent" }
              ],
              "versions": [{ "from": "2001-01-01", "instruments": ["Probe instrument"] }],
              "rows": [{ "from": "2001-01-01", "source": "Probe source", "cells": { "entry": "a", "rate": {{rate}} } }]
            }
            """));

    private static async Task<DateOnly> FirstVersionAsync(StatutoryParametersDbContext context, ParameterSet set) =>
        await context.ParameterVersions.Where(v => v.SetId == set.Id).OrderBy(v => v.From).Select(v => v.From).FirstAsync(TestContext.Current.CancellationToken);

    private async Task SeedAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<StatutoryParameterSeeder>().SeedAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SetIdAsync(string key)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<StatutoryParametersDbContext>().ParameterSets
            .AsNoTracking()
            .SingleAsync(s => s.Key == key, TestContext.Current.CancellationToken)).Id.Value;
    }

    private async Task<IReadOnlyList<RecordedChange>> ChangesOfAsync(Guid setId)
    {
        using var administrator = factory.CreateClient().AsUser(TestUsers.Administrator);

        return await ParameterSetsApi.ChangesOfAsync(administrator, setId);
    }

    private async Task<long[]> ChangeCountsAsync()
    {
        using var administrator = factory.CreateClient().AsUser(TestUsers.Administrator);
        var counts = new List<long>();
        foreach (var key in ChangeKeys)
        {
            counts.Add(await ParameterSetsApi.ChangeCountAsync(administrator, key));
        }

        return [.. counts];
    }

    private async Task<List<string>> TablesAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<StatutoryParametersDbContext>();
        var sets = await context.ParameterSets.AsNoTracking().OrderBy(s => s.Key).ToListAsync(TestContext.Current.CancellationToken);
        var versions = await context.ParameterVersions.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        var rows = await context.ParameterRows.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);

        return
        [
            .. sets.Select(s => $"set {s.Id.Value} {s.Key} {s.Title} {s.ModifiedAt:O}"),
            .. versions.OrderBy(v => v.Id.Value).Select(v => $"version {v.Id.Value} {v.SetId.Value} {v.From:O} {string.Join('|', v.Instruments)} {v.ModifiedAt:O}"),
            .. rows.OrderBy(r => r.Id.Value).Select(r => $"row {r.Id.Value} {r.SetId.Value} {r.RowKey} {r.From:O} {r.Until:O} {r.Source} {r.ModifiedAt:O}"),
        ];
    }
}
