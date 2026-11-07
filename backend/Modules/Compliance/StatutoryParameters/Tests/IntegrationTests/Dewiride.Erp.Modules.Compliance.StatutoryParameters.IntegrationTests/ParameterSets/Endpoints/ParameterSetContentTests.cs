using System.Globalization;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.ParameterSets.Endpoints;

// The answer for every version's date of every seed file is held to the file itself: the entries in force that day, and what the
// version added, changed (each entry as it was the day before and as it is from the version's date) and removed, worked out here
// from the file's rows by key.
[Collection(StatutoryRows.Name)]
public sealed class ParameterSetContentTests(ErpApiFactory factory)
{
    public static TheoryData<string, string> VersionDates()
    {
        var data = new TheoryData<string, string>();
        foreach (var file in SeedFileSets.All)
        {
            foreach (var version in file.Versions)
            {
                data.Add(file.Set.Key, version.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(VersionDates))]
    public async Task Get_OnEachVersionsDate_AnswersTheFilesEntriesAndWhatTheVersionChanged(string key, string day)
    {
        var date = DateOnly.Parse(day, CultureInfo.InvariantCulture);
        var file = SeedFileSets.Of(key);
        var starting = file.Rows.Where(row => row.From == date).ToList();
        var ending = file.Rows.Where(row => row.Until == date.AddDays(-1)).ToList();
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        var shown = (await ParameterSetsApi.GetAsync(client, key, day)).Shown;

        Assert.NotNull(shown);
        Assert.Equal(date, shown.From);
        Assert.Equal(file.Versions.Single(version => version.From == date).Instruments, shown.Instruments);
        Assert.Equal(ByKey(file.Rows.Where(row => row.Span.Contains(date))).Select(Describe), shown.Rows.Select(Describe));
        Assert.Equal(ByKey(starting.Where(row => !ending.Exists(before => before.RowKey == row.RowKey))).Select(Describe), shown.Added.Select(Describe));
        Assert.Equal(
            ByKey(starting.Where(row => ending.Exists(before => before.RowKey == row.RowKey))).Select(after => $"{Describe(ending.Single(before => before.RowKey == after.RowKey))} => {Describe(after)}"),
            shown.Changed.Select(change => $"{Describe(change.Before)} => {Describe(change.After)}"));
        Assert.Equal(ByKey(ending.Where(row => !starting.Exists(after => after.RowKey == row.RowKey))).Select(Describe), shown.Removed.Select(Describe));
    }

    private static IEnumerable<ParameterRow> ByKey(IEnumerable<ParameterRow> rows) => rows.OrderBy(row => row.RowKey, StringComparer.Ordinal);

    private static string Describe(ParameterRow row) =>
        Describe(row.RowKey, row.From, row.Until, row.Source, row.Cells.Values.Select(cell => (cell.Key, cell.Value)));

    private static string Describe(ParameterRowResponse row) =>
        Describe(row.Key, row.From, row.Until, row.Source, row.Cells.Select(cell => (cell.Column, cell.Value)));

    private static string Describe(string key, DateOnly from, DateOnly? until, string source, IEnumerable<(string Column, string? Value)> cells) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{key} from {from:yyyy-MM-dd} until {until:yyyy-MM-dd} by {source}: {string.Join("; ", cells.OrderBy(cell => cell.Column, StringComparer.Ordinal).Select(cell => $"{cell.Column}={cell.Value ?? "null"}"))}");
}
