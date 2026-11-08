using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Company;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.IncomeTax;

// Golden values of a domestic company's income-tax for tax years from 2026-27: the rates of the Finance Act, 2026 and of the options
// of sections 199 to 201 of the Income-tax Act, 2025, the surcharge, the Health and Education Cess and the minimum alternate tax, each
// pinned beside its instrument so that a wrong edit of a seed file fails a test (ADR-0063). The Finance Act's rows are for the tax
// year 2026-27 alone: each is pinned on the last day of that year and is absent from 1 April 2027, when the version that ends them
// begins, while the Act's own rows stay in force.
[Collection(StatutoryRows.Name)]
public sealed class CompanyTaxGoldenTests(ErpApiFactory factory)
{
    private const string RatesKey = "income-tax.company-rates";

    private const string SurchargeKey = "income-tax.company-surcharge";

    private const string CessKey = "income-tax.health-and-education-cess";

    private const string MinimumAlternateTaxKey = "income-tax.minimum-alternate-tax";

    private const string OptionBecomesInvalid = "(i) the option shall become invalid in respect of such tax year and subsequent tax years;";

    private const string SoftwareDevelopment = "(i) development of computer software in any form or in any media";

    private static readonly DateOnly FirstTaxYear = new(2026, 4, 1);

    private static readonly DateOnly LastDayOfFirstTaxYear = new(2027, 3, 31);

    private static readonly DateOnly SecondTaxYear = new(2027, 4, 1);

    [Theory]
    [InlineData("turnover-within-limit", CompanyTaxRegime.FinanceAct, "25", "4000000000", "2024-25", null, null, "does not exceed ₹ 400 crores; 25% of the total income;", "Finance Act, 2026, First Schedule, Part I-B, Paragraph E, item I(i)")]
    [InlineData("other-domestic", CompanyTaxRegime.FinanceAct, "30", null, null, null, null, "(ii) other than that referred to in item (i). 30% of the total income.", "Finance Act, 2026, First Schedule, Part I-B, Paragraph E, item I(ii)")]
    [InlineData("section-199", CompanyTaxRegime.Section199, "25", null, null, "2016-03-01", null, "be computed at the rate of 25% subject to the following conditions:–– (a) the company has been set-up and registered on or after the 1st March, 2016;", "Income-tax Act, 2025, section 199(1)")]
    [InlineData("section-200", CompanyTaxRegime.Section200, "22", null, null, null, null, "the income-tax payable for a tax year shall be at the rate of 22%, at the option of a person being a domestic company", "Income-tax Act, 2025, section 200(1)")]
    [InlineData("section-201-manufacturing", CompanyTaxRegime.Section201, "15", null, null, "2019-10-01", "2024-03-31", "(a) 15% on the total income other than the income mentioned in clauses (b), (c) and (d);", "Income-tax Act, 2025, section 201(1), Table, Sl. No. 1, columns B, C(a) and D")]
    [InlineData("section-201-other-income", CompanyTaxRegime.Section201, "22", null, null, "2019-10-01", "2024-03-31", "(b) 22% (without any deduction or allowance in respect of any expenditure or allowance) on such income,–– (i) which has neither been derived from nor is incidental to manufacturing", "Income-tax Act, 2025, section 201(1), Table, Sl. No. 1, columns B, C(b) and D")]
    [InlineData("section-201-short-term-capital-gains", CompanyTaxRegime.Section201, "22", null, null, "2019-10-01", "2024-03-31", "(c) 22% on short-term capital gains derived from transfer of a capital asset on which no depreciation is allowable under this Act;", "Income-tax Act, 2025, section 201(1), Table, Sl. No. 1, columns B, C(c) and D")]
    [InlineData("section-201-deemed-excess-profits", CompanyTaxRegime.Section201, "30", null, null, "2019-10-01", "2024-03-31", "(d) 30% on the income deemed so under section 205(4).", "Income-tax Act, 2025, section 201(1), Table, Sl. No. 1, columns B, C(d) and D")]
    public async Task Rate_OfEachRegimeInTheFirstTaxYear_IsTheRateAndWindowItsInstrumentSets(string entry, CompanyTaxRegime regime, string rate, string? turnoverLimit, string? turnoverYear, string? setUpOnOrAfter, string? productionBy, string words, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var rates = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<CompanyTaxRates>(FirstTaxYear, TestContext.Current.CancellationToken))!.Rates;

        var found = Assert.Single(rates, candidate => candidate.Entry == entry);
        Assert.Equal(regime, found.Regime);
        Assert.Equal(new Percentage(Number(rate)), found.Rate);
        Assert.Equal(Rupees(turnoverLimit), found.TurnoverLimit);
        Assert.Equal(turnoverYear, found.TurnoverYear);
        Assert.Equal(Day(setUpOnOrAfter), found.SetUpOnOrAfter);
        Assert.Equal(Day(productionBy), found.ProductionBy);
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.Contains(instrument, await SourceAsync(scope, RatesKey, entry), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("turnover-within-limit", null, false)]
    [InlineData("other-domestic", null, false)]
    [InlineData("section-199", "it cannot be subsequently withdrawn for the same or any other tax year, except where the person exercises option under section 200.", true)]
    [InlineData("section-200", "(6) Once the option under this section has been exercised for any tax year, it shall not be subsequently withdrawn for the same or any other tax year.", false)]
    [InlineData("section-201-manufacturing", OptionBecomesInvalid, true)]
    [InlineData("section-201-other-income", OptionBecomesInvalid, true)]
    [InlineData("section-201-short-term-capital-gains", OptionBecomesInvalid, true)]
    [InlineData("section-201-deemed-excess-profits", OptionBecomesInvalid, true)]
    public async Task Option_OfEachRate_IsTheActsWordsAndOnlyTheManufacturingOptionsLeaveOutSoftwareDevelopment(string entry, string? option, bool manufacturing)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var rates = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<CompanyTaxRates>(FirstTaxYear, TestContext.Current.CancellationToken))!.Rates;

        var found = Assert.Single(rates, candidate => candidate.Entry == entry);
        if (option is null)
        {
            Assert.Null(found.Option);
        }
        else
        {
            Assert.Contains(option, found.Option, StringComparison.Ordinal);
        }

        if (manufacturing)
        {
            Assert.Contains(SoftwareDevelopment, found.ExcludedBusinesses, StringComparison.Ordinal);
        }
        else
        {
            Assert.Null(found.ExcludedBusinesses);
        }
    }

    [Theory]
    [InlineData("schedule-rates-above-1-crore", CompanySurchargeTaxOn.ScheduleRates, "7", "10000000", "100000000", "Every domestic company.", "(i) Where the total income exceeds ₹ 10000000 but does not exceed ₹ 100000000, at the rate of 7%;", "Wn = Un + Vn", "Paragraph F, Table 1, Sl. No. 5(i) and Table 2, Sl. No. 5")]
    [InlineData("schedule-rates-above-10-crore", CompanySurchargeTaxOn.ScheduleRates, "12", "100000000", null, "Every domestic company.", "(ii) where the total income exceeds ₹ 100000000, at the rate of 12%.", "Wn = Un + Vn", "Paragraph F, Table 1, Sl. No. 5(ii) and Table 2, Sl. No. 5")]
    [InlineData("special-sections-above-1-crore", CompanySurchargeTaxOn.SpecialSections, "7", "10000000", "100000000", "Every domestic company except such domestic company whose income is chargeable to tax under section 200 or 201 of the said Act.", "(i) Where the total income exceeds ₹ 10000000 but does not exceed ₹ 100000000, at the rate of 7%;", "Tn = Rn + Sn", "section 3(4)(b), Table, Sl. No. 6(i), with section 3(5), Table, Sl. No. 5")]
    [InlineData("special-sections-above-10-crore", CompanySurchargeTaxOn.SpecialSections, "12", "100000000", null, "Every domestic company except such domestic company whose income is chargeable to tax under section 200 or 201 of the said Act.", "(ii) where the total income exceeds ₹ 100000000, at the rate of 12%.", "Tn = Rn + Sn", "section 3(4)(b), Table, Sl. No. 6(ii), with section 3(5), Table, Sl. No. 5")]
    [InlineData("new-regime-options", CompanySurchargeTaxOn.NewRegimeOptions, "10", null, null, "Every domestic company other than a special purpose vehicle referred to in Schedule V [Note 2].", "[Sl. No. 9.B] 200 or 201. [Sl. No. 9.D] 10%.", null, "as substituted by the Taxation and Other Laws (Amendment) Act, 2026, section 6(a) and (b)")]
    [InlineData("new-regime-options-special-purpose-vehicle", CompanySurchargeTaxOn.NewRegimeOptions, "25", null, null, "Every domestic company, being a special purpose vehicle referred to in Schedule V [Note 2].", "[Sl. No. 9A.B] 200 or 201. [Sl. No. 9A.D] 25%.", null, "as substituted by the Taxation and Other Laws (Amendment) Act, 2026, section 6(a) and (b)")]
    public async Task Surcharge_OfEachBandInTheFirstTaxYear_IsTheRateAndBoundsItsInstrumentSets(string band, CompanySurchargeTaxOn taxOn, string rate, string? incomeAbove, string? incomeUpTo, string persons, string words, string? marginalRelief, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var bands = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<CompanySurcharges>(FirstTaxYear, TestContext.Current.CancellationToken))!.Bands;

        var found = Assert.Single(bands, candidate => candidate.Band == band);
        Assert.Equal(taxOn, found.TaxOn);
        Assert.Equal(new Percentage(Number(rate)), found.Rate);
        Assert.Equal(Rupees(incomeAbove), found.IncomeAbove);
        Assert.Equal(Rupees(incomeUpTo), found.IncomeUpTo);
        Assert.Equal(persons, found.Persons);
        Assert.EndsWith(words, found.Wording, StringComparison.Ordinal);
        if (marginalRelief is null)
        {
            Assert.Null(found.MarginalRelief);
        }
        else
        {
            Assert.Contains(marginalRelief, found.MarginalRelief, StringComparison.Ordinal);
        }

        Assert.Contains(instrument, await SourceAsync(scope, SurchargeKey, band), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("charge", "4", "(15) The amount of income-tax as specified in sub-sections (1) to (5) and as increased by the applicable surcharge", "Finance Act, 2026, section 3(15)")]
    [InlineData("advance-tax", "4", "(16) The amount of income-tax as specified in sub-sections (6) to (14) and as increased by the applicable surcharge", "Finance Act, 2026, section 3(16)")]
    public async Task Cess_OnEachTaxInTheFirstTaxYear_IsTheRateOfTheIncomeTaxAndSurchargeItsInstrumentSets(string cess, string rate, string words, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var rates = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<HealthAndEducationCess>(FirstTaxYear, TestContext.Current.CancellationToken))!.Rates;

        var found = Assert.Single(rates, candidate => candidate.Cess == cess);
        Assert.Equal(new Percentage(Number(rate)), found.Rate);
        Assert.StartsWith(words, found.Wording, StringComparison.Ordinal);
        Assert.Contains($"to be called the “Health and Education Cess on income-tax”, calculated at the rate of {rate}% of such income-tax and surcharge", found.Wording, StringComparison.Ordinal);
        Assert.Contains(instrument, await SourceAsync(scope, CessKey, cess), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cess_OnAdvanceTax_HoldsTheExceptionForASpecifiedFundAndNotTheOneForDeductions()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var rates = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<HealthAndEducationCess>(FirstTaxYear, TestContext.Current.CancellationToken))!.Rates;

        var wording = Assert.Single(rates, rate => rate.Cess == "advance-tax").Wording;
        Assert.Contains("(ii) in respect of income-tax as specified in sub-sections (10) to (13), calculated on income, referred to in section 210(1) [Table: Sl. No. 1] of the said Act, of specified fund", wording, StringComparison.Ordinal);
        Assert.DoesNotContain("is paid to a domestic company and any other person who is resident in India", wording, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("when-payable", null, null, "the income-tax payable on the total income as computed under this Act for a tax year is less than the minimum alternate tax payable for such tax year", "Income-tax Act, 2025, section 206(1)(a)")]
    [InlineData("book-profit-rate", "14", null, "(ii) in case of any other company, at a rate of 14%.", "Income-tax Act, 2025, section 206(1)(b)(ii), as amended by the Finance Act, 2026, section 59(a)(i)")]
    [InlineData("book-profit-rate-ifsc-unit", "9", null, "deriving its income solely in convertible foreign exchange, at the rate of 9%;", "Income-tax Act, 2025, section 206(1)(b)(i)")]
    [InlineData("new-regime-options-excluded", null, null, "(ii) who has exercised the option under section 200(5) or section 201(2).", "Income-tax Act, 2025, section 206(1)(q)(ii)")]
    [InlineData("life-insurance-excluded", null, null, "(i) being a company having income accruing or arising from life insurance business referred to in section 194(1) (Table: Sl. No. 6);", "Income-tax Act, 2025, section 206(1)(q)(i)")]
    [InlineData("credit-of-tax-paid", null, null, "(iv) clauses (m), (n), (o) and (p) shall be omitted;", "Finance Act, 2026, section 59(a)(iv)")]
    [InlineData("brought-forward-credit-set-off", "25", null, "such credit brought forward shall be allowed to be set off in any tax year to the extent of 25% of the tax payable on the total income", "as substituted by the Finance Act, 2026, section 59(b)")]
    [InlineData("brought-forward-credit-years", null, 15, "shall not be allowed beyond the fifteenth tax year immediately succeeding the tax year in which the tax credit first became allowable", "Income-tax Act, 2025, section 206(3)(b)(iii)")]
    public async Task Rule_OfMinimumAlternateTaxInTheFirstTaxYear_IsAsItsInstrumentWritesIt(string rule, string? percent, int? taxYears, string words, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var rules = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<MinimumAlternateTax>(FirstTaxYear, TestContext.Current.CancellationToken))!.Rules;

        var found = Assert.Single(rules, candidate => candidate.Rule == rule);
        Assert.Equal(Percent(percent), found.Percent);
        Assert.Equal(taxYears, found.TaxYears);
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.Contains(instrument, await SourceAsync(scope, MinimumAlternateTaxKey, rule), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RatesKey, "turnover-within-limit")]
    [InlineData(RatesKey, "other-domestic")]
    [InlineData(SurchargeKey, "schedule-rates-above-1-crore")]
    [InlineData(SurchargeKey, "schedule-rates-above-10-crore")]
    [InlineData(SurchargeKey, "special-sections-above-1-crore")]
    [InlineData(SurchargeKey, "special-sections-above-10-crore")]
    [InlineData(SurchargeKey, "new-regime-options")]
    [InlineData(SurchargeKey, "new-regime-options-special-purpose-vehicle")]
    [InlineData(CessKey, "charge")]
    [InlineData(CessKey, "advance-tax")]
    public async Task FinanceActRow_OnTheLastDayOfItsTaxYearAndTheFirstDayOfTheNext_IsInForceThenGone(string key, string row)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        Assert.Contains(row, await KeysAsync(store, key, LastDayOfFirstTaxYear));
        Assert.DoesNotContain(row, await KeysAsync(store, key, SecondTaxYear));
    }

    [Fact]
    public async Task Rates_FromTheSecondTaxYear_AreTheOptionsOfSections199To201Alone()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<CompanyTaxRates>(SecondTaxYear, TestContext.Current.CancellationToken);

        Assert.NotNull(set);
        Assert.Equal(SecondTaxYear, set.Version.From);
        Assert.Equal(
            ["section-199", "section-200", "section-201-deemed-excess-profits", "section-201-manufacturing", "section-201-other-income", "section-201-short-term-capital-gains"],
            set.Rates.Select(rate => rate.Entry).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(set.Rates, rate => rate.Regime == CompanyTaxRegime.FinanceAct);
    }

    [Fact]
    public async Task SurchargeAndCess_FromTheSecondTaxYear_HoldNoRowUntilItsFinanceActIsSeeded()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        var surcharge = await store.GetAsOfAsync<CompanySurcharges>(SecondTaxYear, TestContext.Current.CancellationToken);
        var cess = await store.GetAsOfAsync<HealthAndEducationCess>(SecondTaxYear, TestContext.Current.CancellationToken);

        Assert.NotNull(surcharge);
        Assert.Equal(SecondTaxYear, surcharge.Version.From);
        Assert.Empty(surcharge.Bands);
        Assert.NotNull(cess);
        Assert.Equal(SecondTaxYear, cess.Version.From);
        Assert.Empty(cess.Rates);
    }

    [Theory]
    [InlineData(RatesKey, "2026-04-01,2027-04-01")]
    [InlineData(SurchargeKey, "2026-04-01,2027-04-01")]
    [InlineData(CessKey, "2026-04-01,2027-04-01")]
    [InlineData(MinimumAlternateTaxKey, "2026-04-01")]
    public async Task Versions_OfEachSet_AreTheTaxYearsItsInstrumentsGovern(string key, string dates)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, FirstTaxYear, TestContext.Current.CancellationToken);

        Assert.Equal(dates.Split(',').Select(day => DateOnly.Parse(day, CultureInfo.InvariantCulture)).Reverse(), set!.Versions.Select(version => version.From));
    }

    [Theory]
    [InlineData(RatesKey, "2026-04-01", 8, 0, 0, "Finance Act, 2026 (No. 4 of 2026), 30 March 2026, sections 3(1), 3(3) and 3(10)|Income-tax Act, 2025 (No. 30 of 2025), 21 August 2025, sections 199, 200, 201 and 205")]
    [InlineData(RatesKey, "2027-04-01", 0, 0, 2, "Finance Act, 2026 (No. 4 of 2026), long title: “An Act to give effect to the financial proposals of the Central Government for the financial year 2026-27.”|Finance Act, 2026 (No. 4 of 2026), section 3(1): “for the tax year commencing on the 1st day of April, 2026, income-tax shall be charged|Finance Act, 2026 (No. 4 of 2026), section 3(10)(iv)")]
    [InlineData(SurchargeKey, "2026-04-01", 6, 0, 0, "Finance Act, 2026 (No. 4 of 2026), 30 March 2026, sections 3(4), 3(5), 3(12) and 3(13)|Taxation and Other Laws (Amendment) Act, 2026 (No. 21 of 2026), 17 August 2026, section 6")]
    [InlineData(SurchargeKey, "2027-04-01", 0, 0, 6, "Finance Act, 2026 (No. 4 of 2026), long title: “An Act to give effect to the financial proposals of the Central Government for the financial year 2026-27.”|Finance Act, 2026 (No. 4 of 2026), section 3(1): “for the tax year commencing on the 1st day of April, 2026, income-tax shall be charged”|Finance Act, 2026 (No. 4 of 2026), section 3(10)(iv)")]
    [InlineData(CessKey, "2026-04-01", 2, 0, 0, "Finance Act, 2026 (No. 4 of 2026), 30 March 2026, section 3(15), (16) and (17)")]
    [InlineData(CessKey, "2027-04-01", 0, 0, 2, "Finance Act, 2026 (No. 4 of 2026), long title: “An Act to give effect to the financial proposals of the Central Government for the financial year 2026-27.”|Finance Act, 2026 (No. 4 of 2026), section 3(15)|Finance Act, 2026 (No. 4 of 2026), section 3(16)")]
    [InlineData(MinimumAlternateTaxKey, "2026-04-01", 8, 0, 0, "Income-tax Act, 2025 (No. 30 of 2025), 21 August 2025, section 206(1) and (3)|Finance Act, 2026 (No. 4 of 2026), 30 March 2026, section 59")]
    public async Task Version_OfEachSet_CitesItsInstrumentsAndChangesTheRowsTheyChange(string key, string from, int added, int changed, int removed, string instruments)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, FirstTaxYear, TestContext.Current.CancellationToken);

        var version = Assert.Single(set!.Versions, candidate => candidate.From == DateOnly.Parse(from, CultureInfo.InvariantCulture));
        var cited = instruments.Split('|');
        Assert.Equal(cited.Length, version.Instruments.Count);
        Assert.All(cited.Zip(version.Instruments), pair => Assert.StartsWith(pair.First, pair.Second, StringComparison.Ordinal));
        Assert.Equal((added, changed, removed), (version.AddedCount, version.ChangedCount, version.RemovedCount));
    }

    [Fact]
    public async Task CompanyTaxSets_OnTheDayBeforeTheIncomeTaxAct2025CameIntoForce_HoldNothing()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();
        var day = FirstTaxYear.AddDays(-1);

        Assert.Null(await store.GetAsOfAsync<CompanyTaxRates>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<CompanySurcharges>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<HealthAndEducationCess>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<MinimumAlternateTax>(day, TestContext.Current.CancellationToken));
    }

    private static decimal Number(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private static Percentage? Percent(string? value) => value is null ? null : new Percentage(Number(value));

    private static Money? Rupees(string? value) => value is null ? null : new Money(Number(value), Currency.Inr);

    private static DateOnly? Day(string? value) => value is null ? null : DateOnly.Parse(value, CultureInfo.InvariantCulture);

    private static async Task<IReadOnlyList<string>> KeysAsync(IStatutoryParameters store, string key, DateOnly day) => key switch
    {
        RatesKey => [.. (await store.GetAsOfAsync<CompanyTaxRates>(day, TestContext.Current.CancellationToken))!.Rates.Select(rate => rate.Entry)],
        SurchargeKey => [.. (await store.GetAsOfAsync<CompanySurcharges>(day, TestContext.Current.CancellationToken))!.Bands.Select(band => band.Band)],
        CessKey => [.. (await store.GetAsOfAsync<HealthAndEducationCess>(day, TestContext.Current.CancellationToken))!.Rates.Select(rate => rate.Cess)],
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "No Finance Act set reads this key."),
    };

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string key, string rowKey)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, FirstTaxYear, TestContext.Current.CancellationToken);

        return Assert.Single(view!.Rows, row => row.Key == rowKey).Source;
    }
}
