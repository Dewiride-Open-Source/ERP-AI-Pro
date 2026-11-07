using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Services;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Gst;

// Golden values of the rate notifications' Tables, each pinned beside its instrument so that a wrong edit of a seed file fails a
// test (ADR-0063). Each case is an entry on a day at the edge of a rate or a wording: its rate and the words of column (3) as the
// Table prints them, and the instrument its source cites; an entry the Table did not hold that day has no rate, and the
// instrument is then the one the version in force that day cites.
[Collection(StatutoryRows.Name)]
public sealed class ServiceRateGoldenTests(ErpApiFactory factory)
{
    private const string Central = "gst.central-tax-rates";

    private const string Integrated = "gst.integrated-tax-rates";

    private const string UnionTerritory = "gst.union-territory-tax-rates";

    private const string Residual2017 = "Other professional, technical and business services other than (i) above.";

    private const string Residual2019 = "Other professional, technical and business services other than (i) above and serial number 38 below.";

    private const string ResidualBesideIa = "Other professional, technical and business services other than (i) and (ia) above and serial number 38 below.";

    private const string Residual2025 = "Other professional, technical and business services other than (i) above and serial number 38 below";

    private const string Petroleum = "Other professional, technical and business services relating to exploration, mining or drilling of petroleum crude or natural gas or both";

    private const string PrintMedia = "Selling of space for advertisement in print media.";

    private const string RenewableEnergy = "Service by way of construction or engineering or installation or other technical services, provided in relation of setting up of following, - (a) Bio-gas plant (b) Solar power based devices (c) Solar power generating system (d) Wind mills, Wind Operated Electricity Generator (WOEG) (e) Waste to energy plants / devices (f) Ocean waves/tidal waves energy devices/plants";

    private const string SoftwareIp = "Temporary or permanent transfer or permitting the use or enjoyment of Intellectual Property (IP) right in respect of Information Technology software. [Please refer to Explanation no. (v)]";

    private const string AnyIp = "Temporary or permanent transfer or permitting the use or enjoyment of Intellectual Property (IP) right.";

    [Theory]
    [InlineData(Central, "21(ii)", "2017-07-01", "9", Residual2017, "Notification No. 11/2017-Central Tax (Rate)")]
    [InlineData(Central, "21(ii)", "2018-12-31", "9", Residual2017, "Notification No. 11/2017-Central Tax (Rate)")]
    [InlineData(Central, "21(ii)", "2019-01-01", "9", Residual2019, "Notification No. 27/2018-Central Tax (Rate)")]
    [InlineData(Central, "21(ii)", "2019-09-30", "9", Residual2019, "Notification No. 27/2018-Central Tax (Rate)")]
    [InlineData(Central, "21(ii)", "2019-10-01", "9", ResidualBesideIa, "Notification No. 20/2019-Central Tax (Rate)")]
    [InlineData(Central, "21(ii)", "2025-09-21", "9", ResidualBesideIa, "Notification No. 20/2019-Central Tax (Rate)")]
    [InlineData(Central, "21(ii)", "2025-09-22", "9", Residual2025, "Notification No. 15/2025-Central Tax (Rate)")]
    [InlineData(Central, "21(ii)", "2026-11-07", "9", Residual2025, "Notification No. 15/2025-Central Tax (Rate)")]
    [InlineData(Central, "21(ia)", "2019-09-30", null, null, "Notification No. 27/2018-Central Tax (Rate)")]
    [InlineData(Central, "21(ia)", "2019-10-01", "6", Petroleum, "Notification No. 20/2019-Central Tax (Rate)")]
    [InlineData(Central, "21(ia)", "2025-09-21", "6", Petroleum, "Notification No. 20/2019-Central Tax (Rate)")]
    [InlineData(Central, "21(ia)", "2025-09-22", null, null, "Notification No. 15/2025-Central Tax (Rate)")]
    [InlineData(Central, "21(i)", "2017-07-01", "2.5", PrintMedia, "Notification No. 11/2017-Central Tax (Rate)")]
    [InlineData(Central, "21(i)", "2026-11-07", "2.5", PrintMedia, "Notification No. 11/2017-Central Tax (Rate)")]
    [InlineData(Central, "38", "2018-12-31", null, null, "Notification No. 11/2017-Central Tax (Rate)")]
    [InlineData(Central, "38", "2019-01-01", "9", RenewableEnergy, "Notification No. 27/2018-Central Tax (Rate)")]
    [InlineData(Central, "17(ii)", "2021-09-30", "9", SoftwareIp, "Notification No. 11/2017-Central Tax (Rate)")]
    [InlineData(Central, "17(ii)", "2021-10-01", "9", AnyIp, "Notification No. 6/2021-Central Tax (Rate)")]
    [InlineData(Integrated, "21(ii)", "2017-07-01", "18", Residual2017, "Notification No. 8/2017-Integrated Tax (Rate)")]
    [InlineData(Integrated, "21(ii)", "2018-12-31", "18", Residual2017, "Notification No. 8/2017-Integrated Tax (Rate)")]
    [InlineData(Integrated, "21(ii)", "2019-01-01", "18", Residual2019, "Notification No. 28/2018-Integrated Tax (Rate)")]
    [InlineData(Integrated, "21(ii)", "2019-09-30", "18", Residual2019, "Notification No. 28/2018-Integrated Tax (Rate)")]
    [InlineData(Integrated, "21(ii)", "2019-10-01", "18", ResidualBesideIa, "Notification No. 19/2019-Integrated Tax (Rate)")]
    [InlineData(Integrated, "21(ii)", "2025-09-21", "18", ResidualBesideIa, "Notification No. 19/2019-Integrated Tax (Rate)")]
    [InlineData(Integrated, "21(ii)", "2025-09-22", "18", Residual2025, "Notification No. 15/2025-Integrated Tax (Rate)")]
    [InlineData(Integrated, "21(ii)", "2026-11-07", "18", Residual2025, "Notification No. 15/2025-Integrated Tax (Rate)")]
    [InlineData(Integrated, "21(ia)", "2019-09-30", null, null, "Notification No. 28/2018-Integrated Tax (Rate)")]
    [InlineData(Integrated, "21(ia)", "2019-10-01", "12", Petroleum, "Notification No. 19/2019-Integrated Tax (Rate)")]
    [InlineData(Integrated, "21(ia)", "2025-09-21", "12", Petroleum, "Notification No. 19/2019-Integrated Tax (Rate)")]
    [InlineData(Integrated, "21(ia)", "2025-09-22", null, null, "Notification No. 15/2025-Integrated Tax (Rate)")]
    [InlineData(Integrated, "21(i)", "2017-07-01", "5", PrintMedia, "Notification No. 8/2017-Integrated Tax (Rate)")]
    [InlineData(Integrated, "21(i)", "2026-11-07", "5", PrintMedia, "Notification No. 8/2017-Integrated Tax (Rate)")]
    [InlineData(Integrated, "38", "2018-12-31", null, null, "Notification No. 8/2017-Integrated Tax (Rate)")]
    [InlineData(Integrated, "38", "2019-01-01", "18", RenewableEnergy, "Notification No. 28/2018-Integrated Tax (Rate)")]
    [InlineData(Integrated, "17(ii)", "2021-09-30", "18", SoftwareIp, "Notification No. 8/2017-Integrated Tax (Rate)")]
    [InlineData(Integrated, "17(ii)", "2021-10-01", "18", AnyIp, "Notification No. 6/2021-Integrated Tax (Rate)")]
    [InlineData(UnionTerritory, "21(ii)", "2017-07-01", "9", Residual2017, "Notification No. 11/2017-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "21(ii)", "2018-12-31", "9", Residual2017, "Notification No. 11/2017-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "21(ii)", "2019-01-01", "9", Residual2019, "Notification No. 27/2018-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "21(ii)", "2019-09-30", "9", Residual2019, "Notification No. 27/2018-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "21(ii)", "2019-10-01", "9", ResidualBesideIa, "Notification No. 20/2019-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "21(ii)", "2025-09-21", "9", ResidualBesideIa, "Notification No. 20/2019-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "21(ii)", "2025-09-22", "9", Residual2025, "Notification No. 15/2025-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "21(ii)", "2026-11-07", "9", Residual2025, "Notification No. 15/2025-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "21(ia)", "2019-09-30", null, null, "Notification No. 27/2018-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "21(ia)", "2019-10-01", "6", Petroleum, "Notification No. 20/2019-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "21(ia)", "2025-09-21", "6", Petroleum, "Notification No. 20/2019-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "21(ia)", "2025-09-22", null, null, "Notification No. 15/2025-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "21(i)", "2017-07-01", "2.5", PrintMedia, "Notification No. 11/2017-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "21(i)", "2026-11-07", "2.5", PrintMedia, "Notification No. 11/2017-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "38", "2018-12-31", null, null, "Notification No. 11/2017-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "38", "2019-01-01", "9", RenewableEnergy, "Notification No. 27/2018-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "17(ii)", "2021-09-30", "9", SoftwareIp, "Notification No. 11/2017-Union Territory Tax (Rate)")]
    [InlineData(UnionTerritory, "17(ii)", "2021-10-01", "9", AnyIp, "Notification No. 6/2021-Union Territory Tax (Rate)")]
    public async Task Entry_OnADayAtTheEdgeOfItsRateOrWords_IsAsItsInstrumentWritesIt(string key, string entry, string day, string? rate, string? description, string instrument)
    {
        var date = DateOnly.Parse(day, CultureInfo.InvariantCulture);
        await using var scope = factory.Services.CreateAsyncScope();

        var entries = await EntriesAsync(scope.ServiceProvider.GetRequiredService<IStatutoryParameters>(), key, date);
        var version = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, date, TestContext.Current.CancellationToken);

        Assert.NotNull(version);
        var found = entries.SingleOrDefault(candidate => candidate.Entry == entry);
        if (rate is null)
        {
            Assert.Null(found);
            Assert.Contains(version.Instruments, cited => cited.StartsWith(instrument, StringComparison.Ordinal));
            return;
        }

        Assert.NotNull(found);
        Assert.Equal(new Percentage(decimal.Parse(rate, CultureInfo.InvariantCulture)), found.Rate);
        Assert.Equal(description, found.Description);
        Assert.Null(found.Condition);
        Assert.Contains(instrument, Assert.Single(version.Rows, row => row.Key == entry).Source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Central)]
    [InlineData(Integrated)]
    [InlineData(UnionTerritory)]
    public async Task Entries_OnTheDayBeforeTheGstBegan_AreNone(string key)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        Assert.Null(await ReadAsync(scope.ServiceProvider.GetRequiredService<IStatutoryParameters>(), key, new DateOnly(2017, 6, 30)));
    }

    [Theory]
    [InlineData(Central, "9983", "9973", "9954 or 9983 or 9987")]
    [InlineData(Integrated, "9983", "9973", "9954 or 9983 or 9987")]
    [InlineData(UnionTerritory, "9983", "9973", "9954 or 9983 or 9987")]
    public async Task Entries_NameTheHeadingsOfColumnTwo(string key, string residualHeading, string intellectualPropertyHeading, string renewableEnergyHeadings)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var entries = await EntriesAsync(scope.ServiceProvider.GetRequiredService<IStatutoryParameters>(), key, new DateOnly(2026, 11, 7));

        Assert.Equal(residualHeading, Assert.Single(entries, entry => entry.Entry == "21(ii)").Heading);
        Assert.Equal(intellectualPropertyHeading, Assert.Single(entries, entry => entry.Entry == "17(ii)").Heading);
        Assert.Equal(renewableEnergyHeadings, Assert.Single(entries, entry => entry.Entry == "38").Heading);
    }

    [Theory]
    [InlineData(Central)]
    [InlineData(Integrated)]
    [InlineData(UnionTerritory)]
    public async Task Entries_OnEveryVersionsDate_NameFourDigitHeadingsAndNeverASixDigitCode(string key)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, new DateOnly(2026, 11, 7), TestContext.Current.CancellationToken);

        Assert.NotEmpty(set!.Versions);
        foreach (var version in set.Versions)
        {
            Assert.All(
                await EntriesAsync(store, key, version.From),
                entry => Assert.All(entry.Heading.Split(" or "), heading => Assert.Matches("^[0-9]{4}$", heading)));
        }
    }

    private static async Task<IReadOnlyList<ServiceRateEntry>> EntriesAsync(IStatutoryParameters store, string key, DateOnly day) =>
        await ReadAsync(store, key, day) ?? throw new InvalidOperationException($"{key} holds nothing for {day:yyyy-MM-dd}.");

    private static async Task<IReadOnlyList<ServiceRateEntry>?> ReadAsync(IStatutoryParameters store, string key, DateOnly day) => key switch
    {
        Central => (await store.GetAsOfAsync<CentralTaxRates>(day, TestContext.Current.CancellationToken))?.Entries,
        Integrated => (await store.GetAsOfAsync<IntegratedTaxRates>(day, TestContext.Current.CancellationToken))?.Entries,
        UnionTerritory => (await store.GetAsOfAsync<UnionTerritoryTaxRates>(day, TestContext.Current.CancellationToken))?.Entries,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "No rate set reads this key."),
    };
}
