using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Bonus;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Labour;

// Golden values of bonus under Chapter IV of the Code on Wages, 2019: its rules, the Central Government's wage limits, the ERP's
// reading of the States' limits and the prior charges of Appendix D of the Wages (Central) Rules, 2026, each pinned beside its
// instrument so that a wrong edit of a seed file fails a test (ADR-0063). The sets answer for the last day of an accounting year
// from the year 2026-27, so the year 2025-26, which ended on 31 March 2026, finds nothing.
[Collection(StatutoryRows.Name)]
public sealed class BonusGoldenTests(ErpApiFactory factory)
{
    private const string RulesKey = "bonus.rules";

    private const string WageLimitsKey = "bonus.wage-limits";

    private const string ReadingsKey = "bonus.state-wage-limit-readings";

    private const string PriorChargesKey = "bonus.prior-charges";

    private const string Delhi = "07";

    private const string ReadingSource = "Code on Wages, 2019, sections 2(d)(ii), 2(w) and 69(2); Payment of Bonus Act, 1965, sections 2(13) and 12, as the Payment of Bonus (Amendment) Act, 2015, sections 2 and 3(i), amended them";

    private const string DelhiAdvisory = "Labour Department, Government of NCT of Delhi, advisory F.No. 15(46)/Lab/2020/800-805, 2 November 2026, for the accounting year 2025-26: “The wage ceiling of ₹21,000 is still applicable in GNCTD.”";

    private const string ReadingWhy = "Section 69(2) of the Code on Wages, 2019 keeps “any amount of wages provided in any provision” of the repealed Payment of Bonus Act, 1965 in force “to the extent they are not contrary to the provisions of this Code till they are repealed under the corresponding provisions of this Code or by the notification to that effect by the Central Government”, and no notification of this State or Union territory under section 26(1) or (2) is held.";

    private const string DelhiWhy = "The Labour Department of the Government of NCT of Delhi reads it so in its advisory of 2 November 2026: “The wage ceiling of ₹21,000 is still applicable in GNCTD.”";

    private const string EligibilityWords = "In section 2 of the Payment of Bonus Act, 1965 (hereinafter referred to as the principal Act), in clause (13), for the words “ten thousand rupees”, the words “twenty-one thousand rupees” shall be substituted.";

    private const string CalculationWords = "In section 12 of the principal Act,— (i) for the words “three thousand and five hundred rupees” at both the places where they occur, the words “seven thousand rupees or the minimum wage for the scheduled employment, as fixed by the appropriate Government, whichever is higher” shall respectively be substituted;";

    private const string CodeOnWagesChapterIV = "Code on Wages, 2019 (No. 29 of 2019), 8 August 2019, Gazette of India, Extraordinary, Part II, Section 1, No. 48: sections 2(a) and 26 to 41";

    private const string CodeOnWagesSection26 = "Code on Wages, 2019 (No. 29 of 2019), 8 August 2019, Gazette of India, Extraordinary, Part II, Section 1, No. 48: sections 2(d) and 26(1) and (2)";

    private const string EligibilityNotification = "S.O. 4711(E), 25 August 2026, Ministry of Labour and Employment, under section 26(1) of the Code on Wages, 2019, Gazette of India, Extraordinary, Part II, Section 3(ii), No. 4526, 25 August 2026";

    private const string CalculationNotification = "S.O. 4710(E), 25 August 2026, Ministry of Labour and Employment, under section 26(2) of the Code on Wages, 2019, Gazette of India, Extraordinary, Part II, Section 3(ii), No. 4525, 25 August 2026";

    private const string CodeOnWagesSavings = "Code on Wages, 2019 (No. 29 of 2019), 8 August 2019, Gazette of India, Extraordinary, Part II, Section 1, No. 48: sections 2(d), 2(w), 26(1) and (2) and 69";

    private const string AmendingAct = "Payment of Bonus (Amendment) Act, 2015 (No. 6 of 2016), 31 December 2015, Gazette of India, Extraordinary, Part II, Section 1, No. 6, 1 January 2016: sections 2 and 3(i), amending sections 2(13) and 12 of the Payment of Bonus Act, 1965";

    private const string WagesCentralRules = "Wages (Central) Rules, 2026, G.S.R. 343(E), 8 May 2026, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3(i), No. 309: rule 26 and Appendix D, under sections 34(c) and 67(3)(f) of the Code on Wages, 2019";

    private const string WagesCentralRulesCorrigendum = "G.S.R. 629(E), 15 July 2026, Ministry of Labour and Employment, corrigendum to G.S.R. 343(E), Gazette of India, Extraordinary, Part II, Section 3(i), No. 571: in rule 1(1), “for “the Code on Wages” read “the Wages””, so the rules are the Wages (Central) Rules, 2026";

    private static readonly DateOnly FirstDay = new(2026, 4, 1);

    private static readonly DateOnly YearEnd = new(2027, 3, 31);

    [Theory]
    [InlineData("2(a)", null, null, null, null, null, null, null, "04-01", "(a) “accounting year” means the year commencing on the 1st day of April;", "Code on Wages, 2019, section 2(a)")]
    [InlineData("26(1)", null, 25, 3, "100", null, null, null, null, "an annual minimum bonus calculated at the rate of eight and one-third per cent. of the wages earned by the employee or one hundred rupees, whichever is higher", "Code on Wages, 2019, section 26(1): eight and one-third per cent., held as 25/3 per cent.")]
    [InlineData("26(1)-days", null, null, null, null, 30, BonusUnit.Days, BonusBound.AtLeast, null, "who has put in at least thirty days work in an accounting year", "Code on Wages, 2019, section 26(1), “at least thirty days work in an accounting year”")]
    [InlineData("26(3)", "20", null, null, null, null, null, BonusBound.AtMost, null, "subject to a maximum of twenty per cent. of such wages.", "Code on Wages, 2019, section 26(3)")]
    [InlineData("26(5)", "20", null, null, null, null, null, BonusBound.AtMost, null, "the total bonus including the annual minimum bonus referred to in sub-section (1) shall not exceed twenty per cent. of the wages earned by the employee in the accounting year.", "Code on Wages, 2019, section 26(5)")]
    [InlineData("26(6)", null, null, null, null, 5, BonusUnit.AccountingYears, BonusBound.AtMost, null, "(6) In the first five accounting years following the accounting year in which the employer sells the goods", "Code on Wages, 2019, section 26(6), its Explanations 1 and 2 and section 26(9)")]
    [InlineData("26(7)(i)", null, null, null, null, 6, BonusUnit.AccountingYears, null, null, "(i) for the sixth accounting year set on or set off, as the case may be, shall be made", "Code on Wages, 2019, section 26(7)(i)")]
    [InlineData("26(7)(ii)", null, null, null, null, 7, BonusUnit.AccountingYears, null, null, "(ii) for the seventh accounting year set on or set off, as the case may be, shall be made", "Code on Wages, 2019, section 26(7)(ii)")]
    [InlineData("26(8)", null, null, null, null, 8, BonusUnit.AccountingYears, BonusBound.AtLeast, null, "(8) From the eighth accounting year following the accounting year in which the employer sells the goods", "Code on Wages, 2019, section 26(8)")]
    [InlineData("27", null, 25, 3, null, null, null, BonusBound.MoreThan, null, "if such bonus is higher than eight and one third per cent. of the salary or wage of the days such employee has worked in that accounting year, shall be proportionately reduced.", "Code on Wages, 2019, sections 27 and 28: eight and one third per cent., held as 25/3 per cent.")]
    [InlineData("29", null, null, null, null, null, null, null, null, "an employee shall be disqualified from receiving bonus under this Code, if he is dismissed from service for— (a) fraud;", "Code on Wages, 2019, section 29")]
    [InlineData("31(1)-banking-company", "60", null, null, null, null, null, null, null, "an amount equal to sixty per cent. in case of a banking company", "Code on Wages, 2019, section 31(1), “sixty per cent. in case of a banking company”")]
    [InlineData("31(1)-other-establishment", "67", null, null, null, null, null, null, null, "sixty-seven per cent. in case of other establishment, of the available surplus", "Code on Wages, 2019, section 31(1), “sixty-seven per cent. in case of other establishment”")]
    [InlineData("36(1)", "20", null, null, null, null, null, BonusBound.AtMost, null, "subject to a limit of twenty per cent. of the total salary or wage of the employees employed in the establishment in that accounting year,", "Code on Wages, 2019, section 36(1), “subject to a limit of twenty per cent. of the total salary or wage”")]
    [InlineData("36(1)-years", null, null, null, null, 4, BonusUnit.AccountingYears, BonusBound.AtMost, null, "be carried forward for being set on in the succeeding accounting year and so on up to and inclusive of the fourth accounting year", "Code on Wages, 2019, section 36(1), “up to and inclusive of the fourth accounting year”")]
    [InlineData("36(2)", null, null, null, null, 4, BonusUnit.AccountingYears, BonusBound.AtMost, null, "shall be carried forward for being set off in the succeeding accounting year and so on up to and inclusive of the fourth accounting year", "Code on Wages, 2019, section 36(2)")]
    [InlineData("36(4)", null, null, null, null, null, null, null, null, "the amount of set on or set off carried forward from the earliest accounting year shall first be taken into account.", "Code on Wages, 2019, section 36(4)")]
    [InlineData("39(1)", null, null, null, null, 8, BonusUnit.Months, BonusBound.AtMost, null, "by crediting it in the bank account of the employee by his employer within a period of eight months from the close of the accounting year:", "Code on Wages, 2019, section 39(1)")]
    [InlineData("39(1)-proviso", null, null, null, null, 2, BonusUnit.Years, BonusBound.AtMost, null, "so, however, that the total period so extended shall not in any case exceed two years.", "Code on Wages, 2019, section 39(1), proviso")]
    [InlineData("39(2)", null, null, null, null, 1, BonusUnit.Months, BonusBound.AtMost, null, "such bonus shall be paid, within a period of one month from the date on which the award becomes enforceable or the settlement comes into operation", "Code on Wages, 2019, section 39(2)")]
    [InlineData("39(2)-proviso", null, 25, 3, null, 8, BonusUnit.Months, BonusBound.AtMost, null, "the employer shall pay eight and one-third per cent. of the wages earned by the employee as per the provisions of this Code within a period of eight months from the close of the accounting year.", "Code on Wages, 2019, section 39(2), proviso: eight and one-third per cent., held as 25/3 per cent.")]
    [InlineData("40(1)", "20", null, null, null, null, null, BonusBound.AtLeast, null, "the income from such sale or services or both, is not less than twenty per cent. of the gross income of the establishment in public sector for that year", "Code on Wages, 2019, section 40(1) and (2)")]
    [InlineData("41(1)", null, null, null, null, null, null, null, null, "41. (1) Nothing in this Chapter shall apply to— (a) employees employed by the Life Insurance Corporation of India;", "Code on Wages, 2019, section 41(1)")]
    [InlineData("41(2)", null, null, null, null, 20, BonusUnit.Persons, BonusBound.AtLeast, null, "the provisions of this Chapter shall apply to such establishment in which twenty or more persons are employed or were employed on any day during an accounting year.", "Code on Wages, 2019, section 41(2)")]
    public async Task Rule_OfChapterIV_IsTheFigureAndWordsTheCodeWrites(string provision, string? percent, int? percentNumerator, int? percentDenominator, string? amount, int? count, BonusUnit? unit, BonusBound? bound, string? day, string words, string source)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var rules = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<BonusRules>(YearEnd, TestContext.Current.CancellationToken))!.Rules;

        var found = Assert.Single(rules, candidate => candidate.Provision == provision);
        Assert.Equal((Percent(percent), percentNumerator, percentDenominator, Rupees(amount)), (found.Percent, found.PercentNumerator, found.PercentDenominator, found.Amount));
        Assert.Equal((count, unit, bound), (found.Count, found.Unit, found.Bound));
        Assert.Equal(day is null ? (DayAndMonth?)null : DayOf(day), found.Day);
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.Equal(source, await SourceAsync(scope, RulesKey, provision, YearEnd));
    }

    [Fact]
    public async Task MinimumBonus_OfEightAndOneThirdPerCent_IsHeldExactlyAsAFraction()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var rules = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<BonusRules>(YearEnd, TestContext.Current.CancellationToken))!.Rules;

        var thirds = rules.Where(rule => rule.PercentNumerator is not null || rule.PercentDenominator is not null).ToList();
        Assert.Equal(["26(1)", "27", "39(2)-proviso"], thirds.Select(rule => rule.Provision));
        Assert.All(thirds, rule =>
        {
            Assert.Equal(((Percentage?)null, (int?)25, (int?)3), (rule.Percent, rule.PercentNumerator, rule.PercentDenominator));
            Assert.Matches("eight and one.third per cent\\.", rule.Wording);
        });
    }

    [Fact]
    public async Task WageLimits_OfTheCentralGovernment_AreTheAmountsOfSO4711AndSO4710()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var limits = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<BonusWageLimits>(YearEnd, TestContext.Current.CancellationToken))!.Limits;

        var found = Assert.Single(limits);
        Assert.Equal(("central", "Central Government", (string?)null), (found.Government, found.Name, found.StateCode));
        Assert.Equal((new Money(21000m, Currency.Inr), new Money(7000m, Currency.Inr)), (found.EligibilityLimit, found.CalculationWage));
        Assert.Contains("every employee drawing wages not exceeding twenty-one thousand rupees per mensum shall be paid bonus", found.EligibilityWording, StringComparison.Ordinal);
        Assert.Contains("as if his wage was seven thousand rupees per mensum or the minimum wage fixed by the Central Government, whichever is higher.", found.CalculationWording, StringComparison.Ordinal);
        Assert.Equal("S.O. 4711(E) and S.O. 4710(E), 25 August 2026, Ministry of Labour and Employment, under section 26(1) and (2) of the Code on Wages, 2019, each deemed in force from 21 November 2025 (its paragraph 2)", await SourceAsync(scope, WageLimitsKey, "central", YearEnd));
    }

    [Fact]
    public async Task Readings_OfTheStates_HoldEveryStateAndUnionTerritoryButBiharAndCiteDelhisAdvisoryForDelhiAlone()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var readings = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<StateWageLimitReadings>(YearEnd, TestContext.Current.CancellationToken))!.Readings;
        var sources = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(ReadingsKey, YearEnd, TestContext.Current.CancellationToken))!.Rows
            .ToDictionary(row => row.Key, row => row.Source, StringComparer.Ordinal);

        Assert.Equal(
            ["01", "02", "03", "04", "05", "06", "07", "08", "09", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24", "26", "27", "29", "30", "31", "32", "33", "34", "35", "36", "37", "38"],
            readings.Select(reading => reading.StateCode));
        Assert.All(readings, reading =>
        {
            var delhi = reading.StateCode == Delhi;
            Assert.Equal((new Money(21000m, Currency.Inr), new Money(7000m, Currency.Inr)), (reading.EligibilityLimit, reading.CalculationWage));
            Assert.Equal((EligibilityWords, CalculationWords), (reading.EligibilityWording, reading.CalculationWording));
            Assert.Equal(delhi ? ReadingWhy + " " + DelhiWhy : ReadingWhy, reading.Why);
            Assert.Equal(delhi ? ReadingSource + "; " + DelhiAdvisory : ReadingSource, sources[reading.StateCode]);
        });
    }

    [Theory]
    [InlineData("1(i)", BonusEmployerCategory.Company, null, null, "(i) The dividends payable on its preference share capital for the accounting year calculated at the actual rate at which such dividends are payable;", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 1(i)")]
    [InlineData("1(ii)", BonusEmployerCategory.Company, "8.5", null, "(ii) 8.5 percent of its paid up equity share capital as at the commencement of the accounting year;", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 1(ii)")]
    [InlineData("1(iii)", BonusEmployerCategory.Company, "6", null, "(iii) 6 percent of its reserves shown in its balance sheet as at the commencement of the accounting year, including any profits carried forward from the previous accounting year:", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 1(iii)")]
    [InlineData("1-proviso", BonusEmployerCategory.Company, "8.5", null, "the total amount to be deducted under this item shall be 8.5 percent on the aggregate of the value of the net fixed assets and the current assets of the company in India after deducting the amount of its current liabilities", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 1, proviso")]
    [InlineData("2(i)", BonusEmployerCategory.BankingCompany, null, null, "(i) The dividends payable on its preference share capital for the accounting year calculated at the rate at which such dividends are payable;", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 2(i)")]
    [InlineData("2(ii)", BonusEmployerCategory.BankingCompany, "7.5", null, "(ii) 7.5 per cent of its paid up equity share capital as at the commencement of the accounting year;", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 2(ii)")]
    [InlineData("2(iii)", BonusEmployerCategory.BankingCompany, "5", null, "(iii) 5 percent of its reserves shown in its balance sheet as at the commencement of the accounting year, including any profits carried forward from the previous accounting year;", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 2(iii)")]
    [InlineData("2(iv)", BonusEmployerCategory.BankingCompany, null, null, "or (b) to any reserves in India in pursuance of any direction or advice given by the Reserve Bank of India, whichever is higher:", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 2(iv)")]
    [InlineData("2-proviso(i)", BonusEmployerCategory.BankingCompany, null, null, "(i) the dividends payable to its preference shareholders for the accounting year at the rate at which such dividends are payable on such amount as bears the same proportion to its total preference share capital", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 2, proviso, clause (i)")]
    [InlineData("2-proviso(ii)", BonusEmployerCategory.BankingCompany, "7.5", null, "(ii) 7.5 per cent of such amount as bears the same proportion to its total paid up equity share capital as its total working funds in India bear to its total working funds.", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 2, proviso, clause (ii)")]
    [InlineData("2-proviso(iii)", BonusEmployerCategory.BankingCompany, "5", null, "(iii) 5 per cent of such amount as bears the same proportion to its total disclosed reserves as its total working funds in India bear to its total world working funds;", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 2, proviso, clause (iii)")]
    [InlineData("2-proviso(iv)", BonusEmployerCategory.BankingCompany, null, null, "under sub-clause (ii) of clause (b) of sub-section (2) of section 11 of the Banking Regulation Act, 1949 (10 of 1949), not exceeding the amount required under the aforesaid provision to be so deposited.", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 2, proviso, clause (iv)")]
    [InlineData("3(i)", BonusEmployerCategory.Corporation, "8.5", null, "(i) 8.5 per cent of its paid up capital as at the commencement of the accounting year;", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 3(i)")]
    [InlineData("3(ii)", BonusEmployerCategory.Corporation, "6", null, "(ii) 6 per cent of its reserves, if any, shown in its balance sheet as at the commencement of the accounting year", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 3(ii)")]
    [InlineData("4(i)", BonusEmployerCategory.CooperativeSociety, "8.5", null, "(i) 8.5 per cent of the capital invested by such society in its establishment as evidenced from its books of accounts at the commencement of the accounting year;", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 4(i)")]
    [InlineData("4(ii)", BonusEmployerCategory.CooperativeSociety, null, null, "(ii) such sums as has been carried forward in respect of the accounting year to a reserve fund under any law relating to co-operative societies for the time being in force.", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 4(ii)")]
    [InlineData("5", BonusEmployerCategory.OtherEmployer, "8.5", null, "8.5 per cent of the capital invested in the establishment as evidenced from his books of accounts at the commencement of the accounting year:", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 5")]
    [InlineData("5-proviso-1", BonusEmployerCategory.OtherEmployer, null, null, "Chapter XXII-A of the income Tax Act applies, the annuity deposit payable under the provisions of that Chapter during the accounting year shall also be deducted:", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 5, first proviso")]
    [InlineData("5-proviso-2", BonusEmployerCategory.OtherEmployer, "25", "500000", "where such employer is a firm, an amount equal to 25 per cent of the gross profits derived by it from the establishment in respect of the accounting year after deducting depreciation", "(i) the total remuneration payable to all such partners is less than the said 25 per cent the amount payable, subject to a maximum of five lakh rupees to each such partner;", "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 5, second proviso, with its clauses (i) and (ii)")]
    [InlineData("5-proviso-3", BonusEmployerCategory.OtherEmployer, "25", "500000", "(i) an amount equal to 25 per cent of the gross profits derived by such employer from the establishment in respect of the accounting year after deducting depreciation", "(ii) five lakh rupees, whichever is less by way of remuneration to such employer, shall also be deducted.", "Wages (Central) Rules, 2026, rule 26 and Appendix D, item 5, third proviso")]
    [InlineData("explanation", null, null, null, "The expression “reserves” occurring in column (3) against Item Nos. 1(iii), 2(iii) and 3(ii) shall not include any amount set apart for the purpose of—", null, "Wages (Central) Rules, 2026, rule 26 and Appendix D, Explanation")]
    public async Task PriorCharge_OfAppendixD_IsThePercentAndAmountItWrites(string item, BonusEmployerCategory? category, string? percent, string? amount, string words, string? amountWords, string source)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var charges = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<BonusPriorCharges>(YearEnd, TestContext.Current.CancellationToken))!.Charges;

        var found = Assert.Single(charges, candidate => candidate.Item == item);
        Assert.Equal((category, Percent(percent), Rupees(amount)), (found.Category, found.Percent, found.Amount));
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        if (amountWords is not null)
        {
            Assert.Contains(amountWords, found.Wording, StringComparison.Ordinal);
        }

        Assert.Equal(source, await SourceAsync(scope, PriorChargesKey, item, YearEnd));
    }

    [Theory]
    [InlineData(RulesKey, "2026-03-31", null)]
    [InlineData(RulesKey, "2026-04-01", 24)]
    [InlineData(WageLimitsKey, "2026-03-31", null)]
    [InlineData(WageLimitsKey, "2026-04-01", 1)]
    [InlineData(ReadingsKey, "2026-03-31", null)]
    [InlineData(ReadingsKey, "2026-04-01", 35)]
    [InlineData(PriorChargesKey, "2026-05-07", null)]
    [InlineData(PriorChargesKey, "2026-05-08", 21)]
    public async Task Set_OnTheFirstDayOfItsVersionAndTheDayBefore_HoldsItsEntriesFromThatDayAndNothingBefore(string key, string day, int? entries)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        Assert.Equal(entries, await CountAsync(scope.ServiceProvider.GetRequiredService<IStatutoryParameters>(), key, Day(day)));
    }

    [Theory]
    [InlineData(RulesKey, "2026-04-01", ParameterSetBasis.Law, 24, CodeOnWagesChapterIV)]
    [InlineData(WageLimitsKey, "2026-04-01", ParameterSetBasis.Law, 1, CodeOnWagesSection26 + "|" + EligibilityNotification + "|" + CalculationNotification)]
    [InlineData(ReadingsKey, "2026-04-01", ParameterSetBasis.Interpretation, 35, CodeOnWagesSavings + "|" + AmendingAct)]
    [InlineData(PriorChargesKey, "2026-05-08", ParameterSetBasis.Law, 21, WagesCentralRules + "|" + WagesCentralRulesCorrigendum)]
    public async Task Version_OfEachSet_IsItsOneVersionCitingItsInstrumentsInOrder(string key, string from, ParameterSetBasis basis, int added, string instruments)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, FirstDay, TestContext.Current.CancellationToken);

        Assert.NotNull(set);
        Assert.Equal((basis, ParameterAsOf.AccountingYearEnd), (set.Basis, set.AsOf));
        var version = Assert.Single(set.Versions);
        Assert.Equal((Day(from), (DateOnly?)null, added, 0, 0), (version.From, version.Until, version.AddedCount, version.ChangedCount, version.RemovedCount));
        var cited = instruments.Split('|');
        Assert.Equal(cited.Length, version.Instruments.Count);
        Assert.All(cited.Zip(version.Instruments), pair => Assert.StartsWith(pair.First, pair.Second, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Description_OfTheReadings_SaysTheyAreTheERPsReadingAndBiharHasNoRow()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(ReadingsKey, FirstDay, TestContext.Current.CancellationToken);

        Assert.StartsWith("The ERP's reading, not a government table:", set!.Description, StringComparison.Ordinal);
        Assert.Contains("Bihar has no row: notifications of its own under section 26, reported for 30 October 2026, are not yet read.", set.Description, StringComparison.Ordinal);
        Assert.Contains("A State's own notification under section 26 replaces its row in a later release", set.Description, StringComparison.Ordinal);
    }

    private static DateOnly Day(string day) => DateOnly.Parse(day, CultureInfo.InvariantCulture);

    private static DayAndMonth DayOf(string day) => DayAndMonth.TryParse(day, out var parsed) ? parsed : throw new ArgumentException($"{day} is not a day and month.", nameof(day));

    private static Percentage? Percent(string? value) => value is null ? null : new Percentage(decimal.Parse(value, CultureInfo.InvariantCulture));

    private static Money? Rupees(string? value) => value is null ? null : new Money(decimal.Parse(value, CultureInfo.InvariantCulture), Currency.Inr);

    private static async Task<int?> CountAsync(IStatutoryParameters store, string key, DateOnly day) => key switch
    {
        RulesKey => (await store.GetAsOfAsync<BonusRules>(day, TestContext.Current.CancellationToken))?.Rules.Count,
        WageLimitsKey => (await store.GetAsOfAsync<BonusWageLimits>(day, TestContext.Current.CancellationToken))?.Limits.Count,
        ReadingsKey => (await store.GetAsOfAsync<StateWageLimitReadings>(day, TestContext.Current.CancellationToken))?.Readings.Count,
        PriorChargesKey => (await store.GetAsOfAsync<BonusPriorCharges>(day, TestContext.Current.CancellationToken))?.Charges.Count,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "No bonus set reads this key."),
    };

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string key, string rowKey, DateOnly on)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, on, TestContext.Current.CancellationToken);

        return Assert.Single(view!.Rows, row => row.Key == rowKey).Source;
    }
}
