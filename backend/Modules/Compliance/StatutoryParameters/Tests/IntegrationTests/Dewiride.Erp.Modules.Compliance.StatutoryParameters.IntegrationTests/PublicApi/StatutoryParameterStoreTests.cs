using System.Globalization;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Returns;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Services;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.PublicApi;

// Each typed set is read on every one of its versions' dates and on the day before its first, the dates taken from its file.
[Collection(StatutoryRows.Name)]
public sealed class StatutoryParameterStoreTests(ErpApiFactory factory)
{
    private static readonly IReadOnlyList<IndianStateEntry> AnyStateCode =
        [.. Enumerable.Range(0, 100).Select(code => new IndianStateEntry(code.ToString("00", CultureInfo.InvariantCulture), "Any territory", IndianStateKind.State, DateOnly.MinValue, null))];

    private static readonly IReadOnlyList<SeededSet> Files = StatutorySeedFiles.Read(StatutorySeedFiles.Embedded(), AnyStateCode);

    public static TheoryData<string> Keys() => [.. Files.Select(file => file.Set.Key)];

    [Theory]
    [MemberData(nameof(Keys))]
    public async Task GetAsOfAsync_DayBeforeTheFirstVersion_AnswersNothing(string key)
    {
        var first = Files.Single(file => file.Set.Key == key).Versions.Min(version => version.From);

        Assert.Null(await ReadAsync(key, first.AddDays(-1)));
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public async Task GetAsOfAsync_EachVersionsDate_AnswersThatVersionWithTheFilesRowsOfThatDay(string key)
    {
        var file = Files.Single(seeded => seeded.Set.Key == key);
        var dates = file.Versions.Select(version => version.From).Order().ToList();

        for (var index = 0; index < dates.Count; index++)
        {
            var read = await ReadAsync(key, dates[index]);

            Assert.NotNull(read);
            var (version, keys) = read.Value;
            Assert.Equal(dates[index], version.From);
            Assert.Equal(index + 1 < dates.Count ? dates[index + 1].AddDays(-1) : null, version.Until);
            Assert.Equal(file.Versions.Single(declared => declared.From == dates[index]).Instruments, version.Instruments);
            Assert.Equal(file.Rows.Where(row => row.Span.Contains(dates[index])).Select(row => row.RowKey).Order(StringComparer.Ordinal), keys);
        }
    }

    [Fact]
    public async Task GetAsOfAsync_SetNoSeedFileHolds_Throws()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsOfAsync<UnseededSet>(new DateOnly(2021, 1, 1), TestContext.Current.CancellationToken));

        Assert.Contains("test.unseeded", refusal.Message, StringComparison.Ordinal);
    }

    private async Task<(StatutoryParameterVersion Version, IReadOnlyList<string> Keys)?> ReadAsync(string key, DateOnly day)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();
        var cancellationToken = TestContext.Current.CancellationToken;

        return key switch
        {
            "gst.sac-codes" => Of(await store.GetAsOfAsync<SacCodes>(day, cancellationToken), set => (set.Version, [.. set.Codes.Select(code => code.Code)])),
            "gst.sac-rate-entries" => Of(await store.GetAsOfAsync<SacRateEntries>(day, cancellationToken), set => (set.Version, [.. set.Entries.Select(entry => entry.Code)])),
            "gst.central-tax-rates" => Of(await store.GetAsOfAsync<CentralTaxRates>(day, cancellationToken), set => (set.Version, [.. set.Entries.Select(entry => entry.Entry)])),
            "gst.integrated-tax-rates" => Of(await store.GetAsOfAsync<IntegratedTaxRates>(day, cancellationToken), set => (set.Version, [.. set.Entries.Select(entry => entry.Entry)])),
            "gst.union-territory-tax-rates" => Of(await store.GetAsOfAsync<UnionTerritoryTaxRates>(day, cancellationToken), set => (set.Version, [.. set.Entries.Select(entry => entry.Entry)])),
            "gst.return-due-dates" => Of(await store.GetAsOfAsync<GstReturnDueDates>(day, cancellationToken), set => (set.Version, [.. set.Rules.Select(rule => rule.Rule)])),
            "gst.quarterly-return-states" => Of(await store.GetAsOfAsync<QuarterlyReturnStates>(day, cancellationToken), set => (set.Version, [.. set.States.Select(state => state.StateCode)])),
            "gst.return-thresholds" => Of(await store.GetAsOfAsync<GstReturnThresholds>(day, cancellationToken), set => (set.Version, [.. set.Thresholds.Select(threshold => threshold.Threshold)])),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "No typed set reads this key."),
        };
    }

    private static (StatutoryParameterVersion Version, IReadOnlyList<string> Keys)? Of<TSet>(TSet? set, Func<TSet, (StatutoryParameterVersion, IReadOnlyList<string>)> read)
        where TSet : class =>
        set is null ? null : read(set);

    private sealed record UnseededSet(StatutoryParameterVersion Version) : IStatutoryParameterSet<UnseededSet>
    {
        public static string Key => "test.unseeded";

        public static UnseededSet Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version);
    }
}
