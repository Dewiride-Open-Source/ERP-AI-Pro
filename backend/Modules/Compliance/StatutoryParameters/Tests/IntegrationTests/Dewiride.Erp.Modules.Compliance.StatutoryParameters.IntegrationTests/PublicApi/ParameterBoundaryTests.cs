using System.Globalization;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.PublicApi;

// Every row of every seed file is asked for on the day before it starts, its first day, its last day and the day after it ends,
// and the version in force that day must hold exactly the file's rows of that day, so the dates come from the files, never from
// the test. The files are read here without the GST State codes, which the seeder of the test database already checked.
[Collection(StatutoryRows.Name)]
public sealed class ParameterBoundaryTests(ErpApiFactory factory)
{
    private static readonly IReadOnlyList<IndianStateEntry> AnyStateCode =
        [.. Enumerable.Range(0, 100).Select(code => new IndianStateEntry(code.ToString("00", CultureInfo.InvariantCulture), "Any territory", IndianStateKind.State, DateOnly.MinValue, null))];

    private static readonly IReadOnlyList<SeededSet> Files = StatutorySeedFiles.Read(StatutorySeedFiles.Embedded(), AnyStateCode);

    public static TheoryData<string, string> Boundaries()
    {
        var data = new TheoryData<string, string>();
        foreach (var file in Files)
        {
            var days = new SortedSet<DateOnly>();
            foreach (var row in file.Rows)
            {
                days.Add(row.From.AddDays(-1));
                days.Add(row.From);
                if (row.Until is { } until)
                {
                    days.Add(until);
                    days.Add(until.AddDays(1));
                }
            }

            foreach (var day in days)
            {
                data.Add(file.Set.Key, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task GetVersionAsync_DayAtTheEdgeOfARow_HoldsExactlyTheFilesRowsOfThatDay(string key, string day)
    {
        var date = DateOnly.Parse(day, CultureInfo.InvariantCulture);
        var file = Files.Single(seeded => seeded.Set.Key == key);
        await using var scope = factory.Services.CreateAsyncScope();

        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, date, TestContext.Current.CancellationToken);

        if (date < file.Versions.Min(version => version.From))
        {
            Assert.Null(view);
            return;
        }

        Assert.NotNull(view);
        Assert.Equal(
            file.Rows.Where(row => row.Span.Contains(date)).OrderBy(row => row.RowKey, StringComparer.Ordinal).Select(Describe),
            view.Rows.Select(Describe));
    }

    private static string Describe(ParameterRow row) =>
        Describe(row.RowKey, row.From, row.Until, row.Source, row.Cells.Values.Select(cell => (cell.Key, cell.Value)));

    private static string Describe(ParameterRowEntry row) =>
        Describe(row.Key, row.From, row.Until, row.Source, row.Cells.Select(cell => (cell.Column, cell.Value)));

    private static string Describe(string key, DateOnly from, DateOnly? until, string source, IEnumerable<(string Column, string? Value)> cells) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{key} from {from:yyyy-MM-dd} until {until:yyyy-MM-dd} by {source}: {string.Join("; ", cells.OrderBy(cell => cell.Column, StringComparer.Ordinal).Select(cell => $"{cell.Column}={cell.Value ?? "null"}"))}");
}
