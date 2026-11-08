using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.FinanceAct;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.IncomeTax.Tds;

// Golden values of the Finance Act, 2026's rates in force, its surcharge and cess on deductions and the ERP's reading of the Part
// II item each entry at the rates in force takes, each pinned beside its instrument so that a wrong edit of a seed file fails a
// test (ADR-0063). Each row holds on the first and the last day of tax year 2026-27; on the first day of tax year 2027-28 each set
// is in force with no row, so a consumer reads that the year's Finance Act is not yet seeded.
[Collection(StatutoryRows.Name)]
public sealed class TdsFinanceActGoldenTests(ErpApiFactory factory)
{
    private const string RatesInForce = "income-tax.tds-rates-in-force";

    private const string SurchargeAndCess = "income-tax.tds-surcharge-and-cess";

    private const string Readings = "income-tax.tds-rates-in-force-readings";

    private const string PartTwo = "Finance Act, 2026, First Schedule, Part II, item";

    private const string PartTwoSurcharge = "Finance Act, 2026, First Schedule, Part II, Surcharge on income-tax, Table";

    private const string SectionThreeSeven = "Finance Act, 2026, section 3(7), Table";

    private const string LongTitle = "Finance Act, 2026 (4 of 2026), long title: “An Act to give effect to the financial proposals of the Central Government for the financial year 2026-27.”";

    private static readonly DateOnly FirstDay = new(2026, 4, 1);

    private static readonly DateOnly LastDay = new(2027, 3, 31);

    private static readonly DateOnly EndDay = new(2027, 4, 1);

    [Theory]
    [InlineData("1(a)(i)", TdsPayee.ResidentOtherThanCompany, "10", PartTwo)]
    [InlineData("1(a)(ii)", TdsPayee.ResidentOtherThanCompany, "30", PartTwo)]
    [InlineData("1(a)(iii)", TdsPayee.ResidentOtherThanCompany, "30", PartTwo)]
    [InlineData("1(a)(iv)", TdsPayee.ResidentOtherThanCompany, "30", PartTwo)]
    [InlineData("1(a)(v)", TdsPayee.ResidentOtherThanCompany, "2", PartTwo)]
    [InlineData("1(a)(vi)", TdsPayee.ResidentOtherThanCompany, "10", PartTwo)]
    [InlineData("1(a)(vii)", TdsPayee.ResidentOtherThanCompany, "10", PartTwo)]
    [InlineData("1(b)(i)(A)", TdsPayee.NonResidentIndian, "20", PartTwo)]
    [InlineData("1(b)(i)(B)", TdsPayee.NonResidentIndian, "12.5", PartTwo)]
    [InlineData("1(b)(i)(C)", TdsPayee.NonResidentIndian, "12.5", PartTwo)]
    [InlineData("1(b)(i)(D)", TdsPayee.NonResidentIndian, "12.5", PartTwo)]
    [InlineData("1(b)(i)(E)", TdsPayee.NonResidentIndian, "20", PartTwo)]
    [InlineData("1(b)(i)(F)", TdsPayee.NonResidentIndian, "20", PartTwo)]
    [InlineData("1(b)(i)(G)", TdsPayee.NonResidentIndian, "20", PartTwo)]
    [InlineData("1(b)(i)(H)", TdsPayee.NonResidentIndian, "20", PartTwo)]
    [InlineData("1(b)(i)(I)", TdsPayee.NonResidentIndian, "20", PartTwo)]
    [InlineData("1(b)(i)(J)", TdsPayee.NonResidentIndian, "30", PartTwo)]
    [InlineData("1(b)(i)(K)", TdsPayee.NonResidentIndian, "30", PartTwo)]
    [InlineData("1(b)(i)(L)", TdsPayee.NonResidentIndian, "30", PartTwo)]
    [InlineData("1(b)(i)(M)", TdsPayee.NonResidentIndian, "10", PartTwo)]
    [InlineData("1(b)(i)(N)", TdsPayee.NonResidentIndian, "20", PartTwo)]
    [InlineData("1(b)(i)(O)", TdsPayee.NonResidentIndian, "30", PartTwo)]
    [InlineData("1(b)(ii)(A)", TdsPayee.OtherNonResident, "20", PartTwo)]
    [InlineData("1(b)(ii)(B)", TdsPayee.OtherNonResident, "20", PartTwo)]
    [InlineData("1(b)(ii)(C)", TdsPayee.OtherNonResident, "20", PartTwo)]
    [InlineData("1(b)(ii)(D)", TdsPayee.OtherNonResident, "20", PartTwo)]
    [InlineData("1(b)(ii)(E)", TdsPayee.OtherNonResident, "30", PartTwo)]
    [InlineData("1(b)(ii)(F)", TdsPayee.OtherNonResident, "30", PartTwo)]
    [InlineData("1(b)(ii)(G)", TdsPayee.OtherNonResident, "30", PartTwo)]
    [InlineData("1(b)(ii)(H)", TdsPayee.OtherNonResident, "20", PartTwo)]
    [InlineData("1(b)(ii)(I)", TdsPayee.OtherNonResident, "12.5", PartTwo)]
    [InlineData("1(b)(ii)(J)", TdsPayee.OtherNonResident, "12.5", PartTwo)]
    [InlineData("1(b)(ii)(K)", TdsPayee.OtherNonResident, "12.5", PartTwo)]
    [InlineData("1(b)(ii)(L)", TdsPayee.OtherNonResident, "10", PartTwo)]
    [InlineData("1(b)(ii)(M)", TdsPayee.OtherNonResident, "20", PartTwo)]
    [InlineData("1(b)(ii)(N)", TdsPayee.OtherNonResident, "30", PartTwo)]
    [InlineData("2(a)(i)", TdsPayee.DomesticCompany, "10", PartTwo)]
    [InlineData("2(a)(ii)", TdsPayee.DomesticCompany, "30", PartTwo)]
    [InlineData("2(a)(iii)", TdsPayee.DomesticCompany, "30", PartTwo)]
    [InlineData("2(a)(iv)", TdsPayee.DomesticCompany, "30", PartTwo)]
    [InlineData("2(a)(v)", TdsPayee.DomesticCompany, "10", PartTwo)]
    [InlineData("2(b)(i)", TdsPayee.OtherCompany, "30", PartTwo)]
    [InlineData("2(b)(ii)", TdsPayee.OtherCompany, "30", PartTwo)]
    [InlineData("2(b)(iii)", TdsPayee.OtherCompany, "30", PartTwo)]
    [InlineData("2(b)(iv)", TdsPayee.OtherCompany, "20", PartTwo)]
    [InlineData("2(b)(v)", TdsPayee.OtherCompany, "20", PartTwo)]
    [InlineData("2(b)(vi)(A)", TdsPayee.OtherCompany, "50", PartTwo)]
    [InlineData("2(b)(vi)(B)", TdsPayee.OtherCompany, "20", PartTwo)]
    [InlineData("2(b)(vii)(A)", TdsPayee.OtherCompany, "50", PartTwo)]
    [InlineData("2(b)(vii)(B)", TdsPayee.OtherCompany, "20", PartTwo)]
    [InlineData("2(b)(viii)", TdsPayee.OtherCompany, "20", PartTwo)]
    [InlineData("2(b)(ix)", TdsPayee.OtherCompany, "12.5", PartTwo)]
    [InlineData("2(b)(x)", TdsPayee.OtherCompany, "12.5", PartTwo)]
    [InlineData("2(b)(xi)", TdsPayee.OtherCompany, "12.5", PartTwo)]
    [InlineData("2(b)(xii)", TdsPayee.OtherCompany, "10", PartTwo)]
    [InlineData("2(b)(xiii)", TdsPayee.OtherCompany, "20", PartTwo)]
    [InlineData("2(b)(xiv)", TdsPayee.OtherCompany, "35", PartTwo)]
    public async Task RateInForce_OnTheFirstAndLastDayOfTaxYear2026_IsTheRatePartTwoPrints(string item, TdsPayee payee, string percent, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        foreach (var day in new[] { FirstDay, LastDay })
        {
            var rate = Assert.Single((await store.GetAsOfAsync<TdsRatesInForce>(day, TestContext.Current.CancellationToken))!.Rates, candidate => candidate.Item == item);
            Assert.Equal(payee, rate.Payee);
            Assert.Equal(Percent(percent), rate.Percent);
        }

        Assert.DoesNotContain((await store.GetAsOfAsync<TdsRatesInForce>(EndDay, TestContext.Current.CancellationToken))!.Rates, candidate => candidate.Item == item);
        Assert.Equal($"{instrument} {item}", await SourceAsync(scope, RatesInForce, item));
    }

    [Theory]
    [InlineData("PartII-1(i)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "5000000", "10000000", TdsSurchargeScope.IncludingDividendAndCapitalGains, "10", PartTwoSurcharge)]
    [InlineData("PartII-1(ii)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "10000000", "20000000", TdsSurchargeScope.IncludingDividendAndCapitalGains, "15", PartTwoSurcharge)]
    [InlineData("PartII-1(iii)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "20000000", "50000000", TdsSurchargeScope.ExcludingDividendAndCapitalGains, "25", PartTwoSurcharge)]
    [InlineData("PartII-1(iv)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "50000000", null, TdsSurchargeScope.ExcludingDividendAndCapitalGains, "37", PartTwoSurcharge)]
    [InlineData("PartII-1(v)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "20000000", null, TdsSurchargeScope.IncludingNotCoveredAbove, "15", PartTwoSurcharge)]
    [InlineData("PartII-1(vi)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Cap, null, null, TdsSurchargeScope.DividendAndCapitalGainsPart, "15", PartTwoSurcharge)]
    [InlineData("PartII-2(i)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "5000000", "10000000", TdsSurchargeScope.IncludingDividendAndCapitalGains, "10", PartTwoSurcharge)]
    [InlineData("PartII-2(ii)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "10000000", "20000000", TdsSurchargeScope.IncludingDividendAndCapitalGains, "15", PartTwoSurcharge)]
    [InlineData("PartII-2(iii)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "20000000", null, TdsSurchargeScope.ExcludingDividendAndCapitalGains, "25", PartTwoSurcharge)]
    [InlineData("PartII-2(iv)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "20000000", null, TdsSurchargeScope.IncludingNotCoveredAbove, "15", PartTwoSurcharge)]
    [InlineData("PartII-2(v)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Cap, null, null, TdsSurchargeScope.DividendAndCapitalGainsPart, "15", PartTwoSurcharge)]
    [InlineData("PartII-3(i)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "5000000", "10000000", TdsSurchargeScope.AllIncome, "10", PartTwoSurcharge)]
    [InlineData("PartII-3(ii)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "10000000", null, TdsSurchargeScope.AllIncome, "15", PartTwoSurcharge)]
    [InlineData("PartII-4(i)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "10000000", "100000000", TdsSurchargeScope.AllIncome, "7", PartTwoSurcharge)]
    [InlineData("PartII-4(ii)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "100000000", null, TdsSurchargeScope.AllIncome, "12", PartTwoSurcharge)]
    [InlineData("PartII-5", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "10000000", null, TdsSurchargeScope.AllIncome, "12", PartTwoSurcharge)]
    [InlineData("PartII-6(i)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "10000000", "100000000", TdsSurchargeScope.AllIncome, "2", PartTwoSurcharge)]
    [InlineData("PartII-6(ii)", TdsSurchargeProvision.PartTwo, TdsSurchargeKind.Band, "100000000", null, TdsSurchargeScope.AllIncome, "5", PartTwoSurcharge)]
    [InlineData("3(7)-2(i)", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "5000000", "10000000", TdsSurchargeScope.AllIncome, "10", SectionThreeSeven)]
    [InlineData("3(7)-2(ii)", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "10000000", "20000000", TdsSurchargeScope.AllIncome, "15", SectionThreeSeven)]
    [InlineData("3(7)-2(iii)", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "20000000", "50000000", TdsSurchargeScope.AllIncome, "25", SectionThreeSeven)]
    [InlineData("3(7)-2(iv)", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "50000000", null, TdsSurchargeScope.AllIncome, "37", SectionThreeSeven)]
    [InlineData("3(7)-3(i)", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "5000000", "10000000", TdsSurchargeScope.AllIncome, "10", SectionThreeSeven)]
    [InlineData("3(7)-3(ii)", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "10000000", "20000000", TdsSurchargeScope.AllIncome, "15", SectionThreeSeven)]
    [InlineData("3(7)-3(iii)", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "20000000", null, TdsSurchargeScope.AllIncome, "25", SectionThreeSeven)]
    [InlineData("3(7)-4(i)", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "5000000", "10000000", TdsSurchargeScope.AllIncome, "10", SectionThreeSeven)]
    [InlineData("3(7)-4(ii)", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "10000000", null, TdsSurchargeScope.AllIncome, "15", SectionThreeSeven)]
    [InlineData("3(7)-5(i)", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "5000000", "10000000", TdsSurchargeScope.AllIncome, "10", SectionThreeSeven)]
    [InlineData("3(7)-5(ii)", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "10000000", null, TdsSurchargeScope.AllIncome, "15", SectionThreeSeven)]
    [InlineData("3(7)-6(i)", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "10000000", "100000000", TdsSurchargeScope.AllIncome, "7", SectionThreeSeven)]
    [InlineData("3(7)-6(ii)", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "100000000", null, TdsSurchargeScope.AllIncome, "12", SectionThreeSeven)]
    [InlineData("3(7)-7", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "10000000", null, TdsSurchargeScope.AllIncome, "12", SectionThreeSeven)]
    [InlineData("3(7)-8(i)", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "10000000", "100000000", TdsSurchargeScope.AllIncome, "2", SectionThreeSeven)]
    [InlineData("3(7)-8(ii)", TdsSurchargeProvision.Section3Sub7, TdsSurchargeKind.Band, "100000000", null, TdsSurchargeScope.AllIncome, "5", SectionThreeSeven)]
    [InlineData("3(8)", TdsSurchargeProvision.Section3Sub8, TdsSurchargeKind.PartTwoRates, null, null, null, null, "Finance Act, 2026, section 3(8)")]
    [InlineData("3(16)", TdsSurchargeProvision.Section3Sub16, TdsSurchargeKind.Cess, null, null, null, "4", "Finance Act, 2026, section 3(16)")]
    [InlineData("3(17)(i)", TdsSurchargeProvision.Section3Sub16, TdsSurchargeKind.CessNotApplied, null, null, null, null, "Finance Act, 2026, section 3(17)(i)")]
    public async Task SurchargeOrCess_OnTheFirstAndLastDayOfTaxYear2026_IsWhatItsProvisionPrints(
        string clause,
        TdsSurchargeProvision provision,
        TdsSurchargeKind kind,
        string? above,
        string? upTo,
        TdsSurchargeScope? counted,
        string? percent,
        string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        foreach (var day in new[] { FirstDay, LastDay })
        {
            var found = Assert.Single((await store.GetAsOfAsync<TdsSurchargesAndCess>(day, TestContext.Current.CancellationToken))!.Clauses, candidate => candidate.Clause == clause);
            Assert.Equal((provision, kind, counted), (found.Provision, found.Kind, found.Scope));
            Assert.Equal(Rupees(above), found.Above);
            Assert.Equal(Rupees(upTo), found.UpTo);
            Assert.Equal(PercentOrNone(percent), found.Percent);
        }

        Assert.DoesNotContain((await store.GetAsOfAsync<TdsSurchargesAndCess>(EndDay, TestContext.Current.CancellationToken))!.Clauses, candidate => candidate.Clause == clause);
        Assert.StartsWith(instrument, await SourceAsync(scope, SurchargeAndCess, clause), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Section3Sub8_OnTheFirstDayOfTaxYear2026_SendsTheCollectionsItNamesToTheRatesAndSurchargeOfPartTwo()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var clauses = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<TdsSurchargesAndCess>(FirstDay, TestContext.Current.CancellationToken))!.Clauses;

        var found = Assert.Single(clauses, candidate => candidate.Clause == "3(8)");
        Assert.Null(found.Person);
        Assert.Equal("section 393(1) [Table: Sl. No. 8(iv). Note 2, Sl. No. 8(iv). Note 6 and Sl. No. 8(vi). Note 6] and 393(3) [Table: Sl. No. 1. Note 2 and Sl. No. 2. Note 2]", found.AppliesTo);
        Assert.Equal("(8) In cases in which tax has to be collected under section 393(1) [Table: Sl. No. 8(iv). Note 2, Sl. No. 8(iv). Note 6 and Sl. No. 8(vi). Note 6] and 393(3) [Table: Sl. No. 1. Note 2 and Sl. No. 2. Note 2] of the said Act, the collection shall be made at the rates specified in Part II of the First Schedule, and shall be increased by a surcharge, for the purposes of the Union, calculated, in cases wherever prescribed, in the manner provided therein.", found.Words);
    }

    [Theory]
    [InlineData(SurchargeAndCess, "section 3(8), which sends the tax collected on a benefit or perquisite, on a virtual digital asset paid for in kind and on winnings in kind to the rates and surcharge of Part II;")]
    [InlineData(Readings, "Section 393(1)'s readings cover a resident other than a company and a domestic company, not a resident company that is not a domestic company;")]
    [InlineData(Readings, "Section 3(8) sends a benefit or perquisite and a virtual digital asset paid for in kind (393(1) [Table: Sl. No. 8(iv). Note 2, Sl. No. 8(iv). Note 6 and Sl. No. 8(vi). Note 6]) to the rates of Part II, whose item for them is not read here; winnings in kind (393(3), Sl. Nos. 1 and 2, Note 2) take their winnings' items.")]
    public async Task Description_OfEachFinanceActSet_SaysWhatItCoversAndLeavesOut(string key, string words)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, FirstDay, TestContext.Current.CancellationToken);

        Assert.Contains(words, set!.Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("393(1)-1(i)-residentOtherThanCompany", "393(1)-1(i)", TdsPayee.ResidentOtherThanCompany, "1(a)(v)", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 1(i)], read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(1)-1(i)-domesticCompany", "393(1)-1(i)", TdsPayee.DomesticCompany, "2(a)(v)", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 1(i)], read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(1)-5(i)-residentOtherThanCompany-1(a)(vi)", "393(1)-5(i)", TdsPayee.ResidentOtherThanCompany, "1(a)(vi)", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(i)], read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(1)-5(i)-residentOtherThanCompany-1(a)(vii)", "393(1)-5(i)", TdsPayee.ResidentOtherThanCompany, "1(a)(vii)", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(i)], read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(1)-5(i)-domesticCompany", "393(1)-5(i)", TdsPayee.DomesticCompany, "2(a)(v)", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(i)], read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(1)-5(ii)-residentOtherThanCompany", "393(1)-5(ii)", TdsPayee.ResidentOtherThanCompany, "1(a)(i)", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(ii)], read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(1)-5(ii)-domesticCompany", "393(1)-5(ii)", TdsPayee.DomesticCompany, "2(a)(i)", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(ii)], read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(1)-5(iii)-residentOtherThanCompany", "393(1)-5(iii)", TdsPayee.ResidentOtherThanCompany, "1(a)(i)", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(iii)], read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(1)-5(iii)-domesticCompany", "393(1)-5(iii)", TdsPayee.DomesticCompany, "2(a)(i)", "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(iii)], read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(3)-1-residentOtherThanCompany", "393(3)-1", TdsPayee.ResidentOtherThanCompany, "1(a)(ii)", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 1), read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(3)-1-nonResidentIndian", "393(3)-1", TdsPayee.NonResidentIndian, "1(b)(i)(J)", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 1), read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(3)-1-otherNonResident", "393(3)-1", TdsPayee.OtherNonResident, "1(b)(ii)(E)", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 1), read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(3)-1-domesticCompany", "393(3)-1", TdsPayee.DomesticCompany, "2(a)(ii)", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 1), read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(3)-1-otherCompany", "393(3)-1", TdsPayee.OtherCompany, "2(b)(i)", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 1), read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(3)-2-residentOtherThanCompany", "393(3)-2", TdsPayee.ResidentOtherThanCompany, "1(a)(iv)", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 2), read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(3)-2-nonResidentIndian", "393(3)-2", TdsPayee.NonResidentIndian, "1(b)(i)(L)", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 2), read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(3)-2-otherNonResident", "393(3)-2", TdsPayee.OtherNonResident, "1(b)(ii)(G)", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 2), read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(3)-2-domesticCompany", "393(3)-2", TdsPayee.DomesticCompany, "2(a)(iv)", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 2), read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(3)-2-otherCompany", "393(3)-2", TdsPayee.OtherCompany, "2(b)(iii)", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 2), read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(3)-3-residentOtherThanCompany", "393(3)-3", TdsPayee.ResidentOtherThanCompany, "1(a)(iii)", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 3), read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(3)-3-nonResidentIndian", "393(3)-3", TdsPayee.NonResidentIndian, "1(b)(i)(K)", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 3), read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(3)-3-otherNonResident", "393(3)-3", TdsPayee.OtherNonResident, "1(b)(ii)(F)", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 3), read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(3)-3-domesticCompany", "393(3)-3", TdsPayee.DomesticCompany, "2(a)(iii)", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 3), read with the Finance Act, 2026, First Schedule, Part II")]
    [InlineData("393(3)-3-otherCompany", "393(3)-3", TdsPayee.OtherCompany, "2(b)(ii)", "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 3), read with the Finance Act, 2026, First Schedule, Part II")]
    public async Task Reading_OnTheFirstAndLastDayOfTaxYear2026_TakesThePartTwoItemOfItsEntryAndPayee(string reading, string entry, TdsPayee payee, string item, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        foreach (var day in new[] { FirstDay, LastDay })
        {
            var found = Assert.Single((await store.GetAsOfAsync<TdsRateInForceReadings>(day, TestContext.Current.CancellationToken))!.Readings, candidate => candidate.Reading == reading);
            Assert.Equal((entry, payee, item), (found.Entry, found.Payee, found.Item));
            Assert.Equal(payee, Assert.Single((await store.GetAsOfAsync<TdsRatesInForce>(day, TestContext.Current.CancellationToken))!.Rates, rate => rate.Item == item).Payee);
        }

        Assert.DoesNotContain((await store.GetAsOfAsync<TdsRateInForceReadings>(EndDay, TestContext.Current.CancellationToken))!.Readings, candidate => candidate.Reading == reading);
        Assert.Equal($"{instrument}, item {item}", await SourceAsync(scope, Readings, reading));
    }

    [Theory]
    [InlineData(RatesInForce)]
    [InlineData(SurchargeAndCess)]
    [InlineData(Readings)]
    public async Task Versions_OfEachFinanceActSet_AreTheFirstDayOfTaxYear2026AndTheDayAfterItsLast(string key)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, FirstDay, TestContext.Current.CancellationToken);

        Assert.Equal([EndDay, FirstDay], set!.Versions.Select(version => version.From));
    }

    [Theory]
    [InlineData(RatesInForce, "2026-04-01", 57, 0, 0, "Finance Act, 2026 (4 of 2026), 30 March 2026, section 3(7), Table, Sl. No. 1, and First Schedule, Part II, in force 1 April 2026 (section 1(2)(a))|Income-tax Act, 2025 (30 of 2025), 21 August 2025, section 2(90), in force 1 April 2026 (section 1(3))")]
    [InlineData(RatesInForce, "2027-04-01", 0, 0, 57, "Income-tax Act, 2025 (30 of 2025), section 2(90)(a)(iv) and (c): the rates in force for deducting tax under these entries are|" + LongTitle)]
    [InlineData(SurchargeAndCess, "2026-04-01", 37, 0, 0, "Finance Act, 2026 (4 of 2026), 30 March 2026, section 3(7), (8), (16) and (17) and First Schedule, Part II, in force 1 April 2026 (section 1(2)(a))")]
    [InlineData(SurchargeAndCess, "2027-04-01", 0, 0, 37, LongTitle)]
    [InlineData(Readings, "2026-04-01", 24, 0, 0, "Income-tax Act, 2025 (30 of 2025), 21 August 2025, section 393(1) and (3), Tables, in force 1 April 2026 (section 1(3))|Finance Act, 2026 (4 of 2026), 30 March 2026, section 3(7), Table, Sl. No. 1, and First Schedule, Part II, in force 1 April 2026 (section 1(2)(a))")]
    [InlineData(Readings, "2027-04-01", 0, 0, 24, "Income-tax Act, 2025 (30 of 2025), section 2(90)(a)(iv) and (c): the rates in force for deducting tax under these entries are|" + LongTitle)]
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
        Assert.All(view.Removed, row => Assert.Equal(LastDay, row.Until));
    }

    [Theory]
    [InlineData(RatesInForce)]
    [InlineData(SurchargeAndCess)]
    [InlineData(Readings)]
    public async Task EndVersion_OfEachFinanceActSet_QuotesTheFinancialYearTheFinanceActIsFor(string key)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, EndDay, TestContext.Current.CancellationToken);

        Assert.NotNull(view);
        Assert.Contains(LongTitle, view.Instruments);
        Assert.Empty(view.Rows);
        Assert.Null(view.Until);
    }

    [Fact]
    public async Task FinanceActSets_OnTheFirstDayOfTaxYear2026_HoldEveryRowOfTheirInstruments()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        var rates = await store.GetAsOfAsync<TdsRatesInForce>(FirstDay, TestContext.Current.CancellationToken);
        var clauses = await store.GetAsOfAsync<TdsSurchargesAndCess>(FirstDay, TestContext.Current.CancellationToken);
        var readings = await store.GetAsOfAsync<TdsRateInForceReadings>(FirstDay, TestContext.Current.CancellationToken);

        Assert.Equal((57, 37, 24), (rates!.Rates.Count, clauses!.Clauses.Count, readings!.Readings.Count));
        Assert.Equal(LastDay, rates.Version.Until);
        Assert.Equal(LastDay, clauses.Version.Until);
        Assert.Equal(LastDay, readings.Version.Until);
    }

    [Fact]
    public async Task FinanceActSets_OnTheFirstDayOfTaxYear2027_AreInForceWithNoRow()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        var rates = await store.GetAsOfAsync<TdsRatesInForce>(EndDay, TestContext.Current.CancellationToken);
        var clauses = await store.GetAsOfAsync<TdsSurchargesAndCess>(EndDay, TestContext.Current.CancellationToken);
        var readings = await store.GetAsOfAsync<TdsRateInForceReadings>(EndDay, TestContext.Current.CancellationToken);

        Assert.Equal((EndDay, EndDay, EndDay), (rates!.Version.From, clauses!.Version.From, readings!.Version.From));
        Assert.Empty(rates.Rates);
        Assert.Empty(clauses.Clauses);
        Assert.Empty(readings.Readings);
    }

    [Fact]
    public async Task FinanceActSets_OnTheDayBeforeTheIncomeTaxAct2025_HoldNothing()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();
        var day = FirstDay.AddDays(-1);

        Assert.Null(await store.GetAsOfAsync<TdsRatesInForce>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<TdsSurchargesAndCess>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<TdsRateInForceReadings>(day, TestContext.Current.CancellationToken));
    }

    private static Percentage Percent(string percent) => new(decimal.Parse(percent, CultureInfo.InvariantCulture));

    private static Percentage? PercentOrNone(string? percent) => percent is null ? null : Percent(percent);

    private static Money? Rupees(string? rupees) => rupees is null ? null : new Money(decimal.Parse(rupees, CultureInfo.InvariantCulture), Currency.Inr);

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string key, string rowKey)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, FirstDay, TestContext.Current.CancellationToken);

        return Assert.Single(view!.Rows, row => row.Key == rowKey).Source;
    }
}
