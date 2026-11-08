using System.Globalization;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Entries;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Limits;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Rates;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.IncomeTax.Tds;

// Golden versions of the TDS sets of the Income-tax Act, 2025: the dates their instruments brought them into force, the instruments
// each version cites, by their opening words in order, and how many rows each added, changed and removed, each pinned beside its
// instrument so that a wrong edit of a seed file fails a test (ADR-0063).
[Collection(StatutoryRows.Name)]
public sealed class TdsLawVersionGoldenTests(ErpApiFactory factory)
{
    [Theory]
    [InlineData("income-tax.tds-entries", "2026-04-01")]
    [InlineData("income-tax.tds-rates", "2026-04-01")]
    [InlineData("income-tax.tds-higher-rates", "2026-04-01")]
    [InlineData("income-tax.tds-threshold-limits", "2026-04-01")]
    [InlineData("income-tax.tds-no-deduction-cases", "2026-04-01")]
    [InlineData("income-tax.tds-declarations", "2026-04-01,2027-04-01")]
    [InlineData("income-tax.tds-terms", "2026-04-01")]
    public async Task Versions_OfEachSet_AreTheDatesItsInstrumentsCameIntoForce(string key, string dates)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, new DateOnly(2026, 11, 8), TestContext.Current.CancellationToken);

        Assert.Equal(dates.Split(',').Select(day => DateOnly.Parse(day, CultureInfo.InvariantCulture)).Reverse(), set!.Versions.Select(version => version.From));
    }

    [Theory]
    [InlineData("income-tax.tds-entries", "2026-04-01", 54, 0, 0, "Income-tax Act, 2025 (30 of 2025), sections 392 and 393 and Schedule XI|Finance Act, 2026 (4 of 2026), section 84(a)")]
    [InlineData("income-tax.tds-rates", "2026-04-01", 61, 0, 0, "Income-tax Act, 2025 (30 of 2025), sections 392 and 393 and Schedule XI")]
    [InlineData("income-tax.tds-higher-rates", "2026-04-01", 8, 0, 0, "Income-tax Act, 2025 (30 of 2025), section 397(2)|Income-tax Rules, 2026, G.S.R. 198(E), 20 March 2026 (Notification No. 22/2026)")]
    [InlineData("income-tax.tds-threshold-limits", "2026-04-01", 36, 0, 0, "Income-tax Act, 2025 (30 of 2025), sections 392 and 393|Finance Act, 2026 (4 of 2026), section 84(a)")]
    [InlineData("income-tax.tds-no-deduction-cases", "2026-04-01", 25, 0, 0, "Income-tax Act, 2025 (30 of 2025), section 393(4), (5), (8) and (9)|Corrigenda to the Income-tax Act, 2025, Ministry of Law and Justice, 30 January 2026|Finance Act, 2026 (4 of 2026), section 84(b)")]
    [InlineData("income-tax.tds-declarations", "2026-04-01", 3, 0, 0, "Income-tax Act, 2025 (30 of 2025), section 393(6)")]
    [InlineData("income-tax.tds-declarations", "2027-04-01", 1, 0, 0, "Finance Act, 2026 (4 of 2026), section 84(c)")]
    [InlineData("income-tax.tds-terms", "2026-04-01", 8, 0, 0, "Income-tax Act, 2025 (30 of 2025), sections 2(100) and 402|Finance Act, 2026 (4 of 2026), section 90(b)")]
    public async Task Changes_OfAVersion_AreTheRowsItsInstrumentsAddedChangedAndRemoved(string key, string from, int added, int changed, int removed, string instruments)
    {
        var date = DateOnly.Parse(from, CultureInfo.InvariantCulture);
        await using var scope = factory.Services.CreateAsyncScope();

        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, date, TestContext.Current.CancellationToken);

        Assert.NotNull(view);
        Assert.Equal(date, view.From);
        var openings = instruments.Split('|');
        Assert.Equal(openings.Length, view.Instruments.Count);
        Assert.All(openings.Zip(view.Instruments), pair => Assert.StartsWith(pair.First, pair.Second, StringComparison.Ordinal));
        Assert.Equal((added, changed, removed), (view.Added.Count, view.Changed.Count, view.Removed.Count));
    }

    [Fact]
    public async Task Sets_OnTheDayBeforeTheActCameIntoForce_HoldNothing()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();
        var day = new DateOnly(2026, 3, 31);
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Null(await store.GetAsOfAsync<TdsEntries>(day, cancellationToken));
        Assert.Null(await store.GetAsOfAsync<TdsRates>(day, cancellationToken));
        Assert.Null(await store.GetAsOfAsync<TdsHigherRates>(day, cancellationToken));
        Assert.Null(await store.GetAsOfAsync<TdsThresholdLimits>(day, cancellationToken));
        Assert.Null(await store.GetAsOfAsync<TdsNoDeductionCases>(day, cancellationToken));
        Assert.Null(await store.GetAsOfAsync<TdsDeclarations>(day, cancellationToken));
        Assert.Null(await store.GetAsOfAsync<TdsTerms>(day, cancellationToken));
    }
}
