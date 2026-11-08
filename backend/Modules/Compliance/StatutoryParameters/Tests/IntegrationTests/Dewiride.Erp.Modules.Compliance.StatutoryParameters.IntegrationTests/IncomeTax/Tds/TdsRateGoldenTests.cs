using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Entries;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Rates;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.IncomeTax.Tds;

// Golden values of the rates of deduction of sections 392 and 393 of the Income-tax Act, 2025 and of the higher rates of section
// 397(2) and rules 162 and 217 of the Income-tax Rules, 2026, each pinned beside its instrument so that a wrong edit of a seed file
// fails a test (ADR-0063). Each case is a rate on the day the Act came into force: its kind and percentage, null where the Act states
// none, and the provision its source cites.
[Collection(StatutoryRows.Name)]
public sealed class TdsRateGoldenTests(ErpApiFactory factory)
{
    private static readonly DateOnly ActInForce = new(2026, 4, 1);

    [Theory]
    [InlineData("392(1)", "392(1)", TdsRateBasis.AverageRate, null, "Income-tax Act, 2025, section 392(1)")]
    [InlineData("392(2)", "392(2)", TdsRateBasis.AverageRate, null, "Income-tax Act, 2025, section 392(2)(b)")]
    [InlineData("392(3)", "392(3)", TdsRateBasis.RatesInForce, null, "Income-tax Act, 2025, section 392(3)")]
    [InlineData("392(6)(a)", "392(6)(a)", TdsRateBasis.ScheduleXi, null, "Income-tax Act, 2025, section 392(6)(a) and Schedule XI, Part A, paragraph 10")]
    [InlineData("392(6)(b)", "392(6)(b)", TdsRateBasis.AverageRate, null, "Income-tax Act, 2025, section 392(6)(b) and Schedule XI, Part B, paragraph 7(1)")]
    [InlineData("392(7)", "392(7)", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 392(7)")]
    [InlineData("393(1)-1(i)", "393(1)-1(i)", TdsRateBasis.RatesInForce, null, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 1(i)]")]
    [InlineData("393(1)-1(ii)", "393(1)-1(ii)", TdsRateBasis.Fixed, "2", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 1(ii)]")]
    [InlineData("393(1)-2(i)", "393(1)-2(i)", TdsRateBasis.Fixed, "2", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 2(i)]")]
    [InlineData("393(1)-2(ii).D(a)", "393(1)-2(ii)", TdsRateBasis.Fixed, "2", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 2(ii).D(a)]")]
    [InlineData("393(1)-2(ii).D(b)", "393(1)-2(ii)", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 2(ii).D(b)]")]
    [InlineData("393(1)-3(i)", "393(1)-3(i)", TdsRateBasis.Fixed, "1", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 3(i)]")]
    [InlineData("393(1)-3(ii)", "393(1)-3(ii)", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 3(ii)]")]
    [InlineData("393(1)-3(iii)", "393(1)-3(iii)", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 3(iii)]")]
    [InlineData("393(1)-4(i)", "393(1)-4(i)", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(i)]")]
    [InlineData("393(1)-4(ii)", "393(1)-4(ii)", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(ii)]")]
    [InlineData("393(1)-4(iii)", "393(1)-4(iii)", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(iii)]")]
    [InlineData("393(1)-4(iv)", "393(1)-4(iv)", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(iv)]")]
    [InlineData("393(1)-5(i)", "393(1)-5(i)", TdsRateBasis.RatesInForce, null, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(i)]")]
    [InlineData("393(1)-5(ii)", "393(1)-5(ii)", TdsRateBasis.RatesInForce, null, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(ii)]")]
    [InlineData("393(1)-5(iii)", "393(1)-5(iii)", TdsRateBasis.RatesInForce, null, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(iii)]")]
    [InlineData("393(1)-6(i).D(a)", "393(1)-6(i)", TdsRateBasis.Fixed, "1", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(i).D(a)] and the Note below serial number 6")]
    [InlineData("393(1)-6(i).D(b)", "393(1)-6(i)", TdsRateBasis.Fixed, "2", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(i).D(b)] and the Note below serial number 6")]
    [InlineData("393(1)-6(ii)", "393(1)-6(ii)", TdsRateBasis.Fixed, "2", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(ii)]")]
    [InlineData("393(1)-6(iii).D(a)", "393(1)-6(iii)", TdsRateBasis.Fixed, "2", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(iii).D(a)]")]
    [InlineData("393(1)-6(iii).D(b)", "393(1)-6(iii)", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(iii).D(b)]")]
    [InlineData("393(1)-7", "393(1)-7", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(1) (Table: Sl. No. 7)")]
    [InlineData("393(1)-8(i)", "393(1)-8(i)", TdsRateBasis.Fixed, "2", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(i)]")]
    [InlineData("393(1)-8(ii)", "393(1)-8(ii)", TdsRateBasis.Fixed, "0.1", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(ii)] and Note 1(b) below serial number 8")]
    [InlineData("393(1)-8(iii)", "393(1)-8(iii)", TdsRateBasis.RatesInForce, null, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(iii)]")]
    [InlineData("393(1)-8(iv)", "393(1)-8(iv)", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(iv)]")]
    [InlineData("393(1)-8(v)", "393(1)-8(v)", TdsRateBasis.Fixed, "0.1", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(v)]")]
    [InlineData("393(1)-8(vi)", "393(1)-8(vi)", TdsRateBasis.Fixed, "1", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(vi)]")]
    [InlineData("393(2)-1", "393(2)-1", TdsRateBasis.Fixed, "20", "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 1)")]
    [InlineData("393(2)-2", "393(2)-2", TdsRateBasis.Fixed, "5", "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 2) and Note 1 of the Table")]
    [InlineData("393(2)-3", "393(2)-3", TdsRateBasis.Fixed, "5", "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 3) and Note 1 of the Table")]
    [InlineData("393(2)-4.E(a)", "393(2)-4", TdsRateBasis.Fixed, "4", "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 4.E(a)) and Note 1 of the Table")]
    [InlineData("393(2)-4.E(b)", "393(2)-4", TdsRateBasis.Fixed, "9", "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 4.E(b)) and Note 1 of the Table")]
    [InlineData("393(2)-5", "393(2)-5", TdsRateBasis.Fixed, "5", "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 5)")]
    [InlineData("393(2)-6.E(a)", "393(2)-6", TdsRateBasis.Fixed, "5", "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 6.E(a))")]
    [InlineData("393(2)-6.E(b)", "393(2)-6", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 6.E(b))")]
    [InlineData("393(2)-7", "393(2)-7", TdsRateBasis.RatesInForce, null, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 7)")]
    [InlineData("393(2)-8", "393(2)-8", TdsRateBasis.RatesInForce, null, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 8)")]
    [InlineData("393(2)-9", "393(2)-9", TdsRateBasis.RatesInForce, null, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 9)")]
    [InlineData("393(2)-10.Note2(a)", "393(2)-10", TdsRateBasis.Fixed, "20", "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 10) and Note 2(a) of the Table")]
    [InlineData("393(2)-10.Note2(b)", "393(2)-10", TdsRateBasis.Agreement, null, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 10) and Note 2(b) of the Table")]
    [InlineData("393(2)-11", "393(2)-11", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 11)")]
    [InlineData("393(2)-12", "393(2)-12", TdsRateBasis.Fixed, "12.5", "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 12)")]
    [InlineData("393(2)-13", "393(2)-13", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 13)")]
    [InlineData("393(2)-14", "393(2)-14", TdsRateBasis.Fixed, "12.5", "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 14)")]
    [InlineData("393(2)-15.Note2(a)", "393(2)-15", TdsRateBasis.Fixed, "20", "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 15) and Note 2(a) of the Table")]
    [InlineData("393(2)-15.Note2(b)", "393(2)-15", TdsRateBasis.Agreement, null, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 15) and Note 2(b) of the Table")]
    [InlineData("393(2)-16", "393(2)-16", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 16)")]
    [InlineData("393(2)-17", "393(2)-17", TdsRateBasis.RatesInForce, null, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 17)")]
    [InlineData("393(3)-1", "393(3)-1", TdsRateBasis.RatesInForce, null, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 1)")]
    [InlineData("393(3)-2", "393(3)-2", TdsRateBasis.RatesInForce, null, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 2) and section 393(3)(a)")]
    [InlineData("393(3)-3", "393(3)-3", TdsRateBasis.RatesInForce, null, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 3)")]
    [InlineData("393(3)-4", "393(3)-4", TdsRateBasis.Fixed, "2", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 4)")]
    [InlineData("393(3)-5", "393(3)-5", TdsRateBasis.Fixed, "2", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 5)")]
    [InlineData("393(3)-6", "393(3)-6", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 6)")]
    [InlineData("393(3)-7", "393(3)-7", TdsRateBasis.Fixed, "10", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 7)")]
    public async Task Rate_OnTheDayTheActCameIntoForce_IsAsTheActPrintsIt(string rate, string entry, TdsRateBasis basis, string? percent, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var rates = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<TdsRates>(ActInForce, TestContext.Current.CancellationToken))!.Rates;

        var found = Assert.Single(rates, candidate => candidate.Rate == rate);
        Assert.Equal((entry, basis, PercentOf(percent)), (found.Entry, found.Basis, found.Percent));
        Assert.Contains(instrument, await SourceAsync(scope, TdsRates.Key, rate), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rates_OnTheDayTheActCameIntoForce_GiveEveryEntryARate()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        var entries = (await store.GetAsOfAsync<TdsEntries>(ActInForce, TestContext.Current.CancellationToken))!.Entries;
        var rates = (await store.GetAsOfAsync<TdsRates>(ActInForce, TestContext.Current.CancellationToken))!.Rates;

        Assert.Equal(61, rates.Count);
        Assert.Equal(entries.Select(entry => entry.Entry).Order(StringComparer.Ordinal), rates.Select(rate => rate.Entry).Distinct().Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("397(2)(b)(i)-goods-and-e-commerce", TdsHigherRateEffect.HigherOf, "5", "Income-tax Act, 2025, section 397(2)(b)(i)(C)")]
    [InlineData("397(2)(b)(i)-other-cases", TdsHigherRateEffect.HigherOf, "20", "Income-tax Act, 2025, section 397(2)(b)(i)(C)")]
    [InlineData("397(2)(c)(i)", TdsHigherRateEffect.NotApplied, null, "Income-tax Act, 2025, section 397(2)(c)(i)")]
    [InlineData("rule-217(1)", TdsHigherRateEffect.NotApplied, null, "Income-tax Rules, 2026, rule 217(1) and (2), under section 397(2)(c)(ii) of the Income-tax Act, 2025")]
    [InlineData("rule-217(3)", TdsHigherRateEffect.NotApplied, null, "Income-tax Rules, 2026, rule 217(3), under section 397(2)(c)(ii) of the Income-tax Act, 2025")]
    [InlineData("397(2)(e)", TdsHigherRateEffect.Cap, null, "Income-tax Act, 2025, section 397(2)(e)")]
    [InlineData("397(2)(f)-(g)", TdsHigherRateEffect.Invalid, null, "Income-tax Act, 2025, section 397(2)(f) and (g)")]
    [InlineData("rule-162(3)(c)", TdsHigherRateEffect.HigherOf, null, "Income-tax Rules, 2026, rule 162(1) to (3)")]
    public async Task HigherRate_OnTheDayTheActCameIntoForce_IsAsTheActOrTheRulesPrintIt(string key, TdsHigherRateEffect effect, string? percent, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var cases = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<TdsHigherRates>(ActInForce, TestContext.Current.CancellationToken))!.Cases;

        var found = Assert.Single(cases, candidate => candidate.Case == key);
        Assert.Equal((effect, PercentOf(percent)), (found.Effect, found.Percent));
        Assert.Contains(instrument, await SourceAsync(scope, TdsHigherRates.Key, key), StringComparison.Ordinal);
    }

    private static Percentage? PercentOf(string? percent) => percent is null ? null : new Percentage(decimal.Parse(percent, CultureInfo.InvariantCulture));

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string key, string rowKey)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, ActInForce, TestContext.Current.CancellationToken);

        return Assert.Single(view!.Rows, row => row.Key == rowKey).Source;
    }
}
