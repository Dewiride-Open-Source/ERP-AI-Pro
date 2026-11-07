using System.Globalization;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Gst;

// Golden versions of every set: the dates its instruments brought them into force, and what each changed (owner decision of
// 2026-11-07: each pinned value beside its instrument).
[Collection(StatutoryRows.Name)]
public sealed class VersionChangeGoldenTests(ErpApiFactory factory)
{
    [Theory]
    [InlineData("gst.central-tax-rates", "2017-07-01,2019-01-01,2019-10-01,2021-10-01,2025-09-22")]
    [InlineData("gst.integrated-tax-rates", "2017-07-01,2019-01-01,2019-10-01,2021-10-01,2025-09-22")]
    [InlineData("gst.union-territory-tax-rates", "2017-07-01,2019-01-01,2019-10-01,2021-10-01,2025-09-22")]
    [InlineData("gst.sac-codes", "2017-07-01,2019-10-01")]
    [InlineData("gst.sac-rate-entries", "2017-07-01")]
    [InlineData("gst.return-due-dates", "2021-01-01")]
    [InlineData("gst.quarterly-return-states", "2021-01-01")]
    [InlineData("gst.return-thresholds", "2021-01-01")]
    public async Task Versions_OfEachSet_AreTheDatesItsInstrumentsCameIntoForce(string key, string dates)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, new DateOnly(2026, 11, 7), TestContext.Current.CancellationToken);

        Assert.Equal(dates.Split(',').Select(day => DateOnly.Parse(day, CultureInfo.InvariantCulture)).Reverse(), set!.Versions.Select(version => version.From));
    }

    [Theory]
    [InlineData("gst.central-tax-rates", "2017-07-01", "17(ii),21(i),21(ii)", "", "", "Notification No. 11/2017-Central Tax (Rate), 28 June 2017, G.S.R. 690(E)")]
    [InlineData("gst.central-tax-rates", "2019-01-01", "38", "21(ii)", "", "Notification No. 27/2018-Central Tax (Rate), 31 December 2018")]
    [InlineData("gst.central-tax-rates", "2019-10-01", "21(ia)", "21(ii)", "", "Notification No. 20/2019-Central Tax (Rate), 30 September 2019, G.S.R. 731(E)")]
    [InlineData("gst.central-tax-rates", "2021-10-01", "", "17(ii)", "", "Notification No. 6/2021-Central Tax (Rate), 30 September 2021, G.S.R. 687(E)")]
    [InlineData("gst.central-tax-rates", "2025-09-22", "", "21(ii)", "21(ia)", "Notification No. 15/2025-Central Tax (Rate), 17 September 2025, G.S.R. 663(E)")]
    [InlineData("gst.integrated-tax-rates", "2017-07-01", "17(ii),21(i),21(ii)", "", "", "Corrigendum of 30 June 2017 to Notification No. 8/2017-Integrated Tax (Rate)")]
    [InlineData("gst.integrated-tax-rates", "2019-01-01", "38", "21(ii)", "", "Notification No. 28/2018-Integrated Tax (Rate), 31 December 2018")]
    [InlineData("gst.integrated-tax-rates", "2019-10-01", "21(ia)", "21(ii)", "", "Notification No. 19/2019-Integrated Tax (Rate), 30 September 2019, G.S.R. 730(E)")]
    [InlineData("gst.integrated-tax-rates", "2021-10-01", "", "17(ii)", "", "Notification No. 6/2021-Integrated Tax (Rate), 30 September 2021, G.S.R. 689(E)")]
    [InlineData("gst.integrated-tax-rates", "2025-09-22", "", "21(ii)", "21(ia)", "Notification No. 15/2025-Integrated Tax (Rate), 17 September 2025, G.S.R. 664(E)")]
    [InlineData("gst.union-territory-tax-rates", "2017-07-01", "17(ii),21(i),21(ii)", "", "", "Corrigendum of 30 June 2017 to Notification No. 11/2017-Union Territory Tax (Rate)")]
    [InlineData("gst.union-territory-tax-rates", "2019-01-01", "38", "21(ii)", "", "Notification No. 27/2018-Union Territory Tax (Rate), 31 December 2018")]
    [InlineData("gst.union-territory-tax-rates", "2019-10-01", "21(ia)", "21(ii)", "", "Notification No. 20/2019-Union Territory Tax (Rate), 30 September 2019, G.S.R. 732(E)")]
    [InlineData("gst.union-territory-tax-rates", "2021-10-01", "", "17(ii)", "", "Notification No. 6/2021-Union Territory Tax (Rate), 30 September 2021, G.S.R. 691(E)")]
    [InlineData("gst.union-territory-tax-rates", "2025-09-22", "", "21(ii)", "21(ia)", "Notification No. 15/2025-Union Territory Tax (Rate), 17 September 2025, G.S.R. 665(E)")]
    [InlineData("gst.sac-codes", "2019-10-01", "", "9973", "", "Notification No. 20/2019-Central Tax (Rate), 30 September 2019, G.S.R. 731(E)")]
    public async Task Changes_OfAVersion_AreTheEntriesItsInstrumentAddedChangedAndRemoved(string key, string from, string added, string changed, string removed, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, DateOnly.Parse(from, CultureInfo.InvariantCulture), TestContext.Current.CancellationToken);

        Assert.NotNull(view);
        Assert.Equal(DateOnly.Parse(from, CultureInfo.InvariantCulture), view.From);
        Assert.Contains(view.Instruments, cited => cited.StartsWith(instrument, StringComparison.Ordinal));
        Assert.Equal(Keys(added), view.Added.Select(row => row.Key));
        Assert.Equal(Keys(changed), view.Changed.Select(change => change.After.Key));
        Assert.Equal(Keys(removed), view.Removed.Select(row => row.Key));
        Assert.All(view.Changed, change => Assert.Equal(view.From.AddDays(-1), change.Before.Until));
        Assert.All(view.Removed, row => Assert.Equal(view.From.AddDays(-1), row.Until));
    }

    [Fact]
    public async Task Description_OfTheErpsReading_CitesTheCorrigendaThatTieTheTaxesToOneAnnexure()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync("gst.sac-rate-entries", new DateOnly(2026, 11, 7), TestContext.Current.CancellationToken);

        Assert.Contains("the corrigenda of 30 June 2017 to Notification No. 8/2017-Integrated Tax (Rate) and Notification No. 11/2017-Union Territory Tax (Rate)", set!.Description, StringComparison.Ordinal);
    }

    private static string[] Keys(string keys) => keys.Length == 0 ? [] : keys.Split(',');
}
