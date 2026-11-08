using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.WelfareFund;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Labour;

// Golden values of the labour welfare funds from 1 April 2026: each State's contributions as its Act or rules fix them, the days
// they and their statements fall due and who each Act covers, each pinned beside its instrument so that a wrong edit of a seed
// file fails a test (ADR-0063). Haryana's limit is indexed each 1 January, so its contribution is in force on 31 December 2026 and
// gone on 1 January 2027 while every other row runs on, and a State whose instrument was not read has no row on any day.
[Collection(StatutoryRows.Name)]
public sealed class WelfareFundGoldenTests(ErpApiFactory factory)
{
    private const string ContributionsKey = "labour-welfare-fund.contributions";

    private const string DueDaysKey = "labour-welfare-fund.due-days";

    private const string CoverageKey = "labour-welfare-fund.coverage";

    private const string HaryanaContributions = "Punjab Labour Welfare Fund Act, 1965 (Punjab Act 17 of 1965), section 9A as in force in Haryana, sub-section (1) as substituted by Haryana Act No. 18 of 2019, 14 March 2019, with the limit of “rupees thirty-five” that the Labour Department's consolidated text of the Act prints in 2026";

    private const string DelhiContributions = "Bombay Labour Welfare Fund Act, 1953 (Bombay Act XL of 1953), section 6BB, as extended to the Union territory of Delhi with modifications by G.S.R. 1286(E), 15 December 1986";

    private const string DelhiFund = "Notification No. F.9(1)/89/DLC(W)/100, Labour Department, Government of the National Capital Territory of Delhi, 13 July 2004, constituting the Labour Welfare Fund of Delhi “with immediate effect”";

    private const string OdishaContributions = "Odisha Labour Welfare Fund Rules, 2015, rule 6, Notification S.R.O. No. 73/2016, Labour & ESI Department, 27 January 2016, under section 34 of the Odisha Labour Welfare Fund Act, 2005 (Odisha Act 14 of 2005), Odisha Gazette, Extraordinary, No. 224, 28 January 2016: “They shall come into force on the date of their publication in the Odisha Gazette.”";

    private const string GujaratContributions = "Notification No. KHR-196-2016-KKB-10-2016-190722-M.4, Labour and Employment Department, Government of Gujarat, 10 November 2016, under section 6(B) of the Labour Welfare Fund Act, 1953 and the Labour Welfare Fund (Gujarat Amendment) Act, 2005, Gujarat Government Gazette, Extraordinary, Part IV-B, No. 634, 15 November 2016, stating no day of commencement";

    private const string MaharashtraContributions = "Maharashtra Labour Welfare Fund Act, 1953 (Mah. XL of 1953), section 6BB, inserted by Mah. 16 of 1971, sub-section (8) as amended by Mah. 24 of 2003, which gives the State Government's share of twice the employee's contribution “with effect from the 1st April 2003”";

    private const string MaharashtraAmendment = "Maharashtra Labour Welfare Fund (Amendment) Act, 2024, section 2, substituting section 6BB(2), Maharashtra Government Gazette, Extraordinary, Part VIII, No. 41, 18 March 2024, its Bill (L.A. Bill No. X of 2024) having no commencement clause";

    private const string KarnatakaContributions = "Karnataka Labour Welfare Fund Act, 1965 (Karnataka Act 15 of 1965), section 7A, inserted by Act No. 73 of 1976, 8 October 1976";

    private const string KarnatakaAmendment = "Karnataka Labour Welfare Fund (Amendment) Act, 2024 (Karnataka Act No. 05 of 2025), section 2, assented to on 8 January 2025, Karnataka Gazette, Extraordinary, Part IV-A, No. 19, 10 January 2025: “It shall come into force at once.”";

    private const string GoaDeduction = "Goa Labour Welfare Fund Act, 1986 (Act 4 of 1987), section 14(2), assented to by the President of India on 4 April 1987, Official Gazette, Series I, No. 5, 30 April 1987";

    private const string GoaAmendment = "Goa Labour Welfare Fund (Amendment) Act, 2015 (Goa Act 9 of 2015), section 6, substituting section 14(1), assented to on 26 August 2015, Official Gazette, Series I, No. 22, Extraordinary No. 2, 28 August 2015: “It shall come into force at once.”";

    private const string KeralaContributions = "Kerala Labour Welfare Fund Act, 1975 (11 of 1977), section 15";

    private const string KeralaAmendment = "Kerala Labour Welfare Fund (Amendment) Act, 2021, section 2, substituting “forty five rupees” in section 15(1), read in its Bill as passed by the Assembly (KNPP. 1989/2021): “It shall be deemed to have come into force on the 16th day of January, 2020.”";

    private const string TelanganaContributions = "Telangana Labour Welfare Fund Act, 1987 (Act No. 34 of 1987), section 10(1), as the Labour Department of Telangana publishes it";

    private const string HaryanaIndexing = "Punjab Labour Welfare Fund Act, 1965 (Punjab Act 17 of 1965), as amended in Haryana, proviso to section 9A(1): “Provided that the limit specified above shall be indexed annually to the consumer price index beginning from first of January each year.”";

    private const string HaryanaDueDay = "Punjab Labour Welfare Fund Act, 1965 (Punjab Act 17 of 1965), section 9A(2) as substituted in Haryana by Haryana Act No. 16 of 2021, 5 April 2021, in the Labour Department's consolidated text";

    private const string DelhiDueDays = "Bombay Labour Welfare Fund Act, 1953 (Bombay Act XL of 1953), section 6BB(3), as extended to the Union territory of Delhi with modifications by G.S.R. 1286(E), 15 December 1986";

    private const string OdishaDueDays = "Odisha Labour Welfare Fund Rules, 2015, rule 6(2), Notification S.R.O. No. 73/2016, Labour & ESI Department, 27 January 2016, Odisha Gazette, Extraordinary, No. 224, 28 January 2016: “They shall come into force on the date of their publication in the Odisha Gazette.”";

    private const string MaharashtraStatements = "Maharashtra Labour Welfare Fund Rules, 1953, rule 3-A, inserted by Government Notification of 4 May 1971, its day of July as notification No. MLW-1161/157706-Lab. iii-B, 18 August 1973, sets it";

    private const string MaharashtraDueDays = "Maharashtra Labour Welfare Fund Act, 1953 (Mah. XL of 1953), section 6BB(3), inserted by Mah. 16 of 1971";

    private const string KarnatakaStatement = "Karnataka Labour Welfare Fund Rules, 1968, rule 3-A(1)";

    private const string KarnatakaDueDay = "Karnataka Labour Welfare Fund Act, 1965 (Karnataka Act 15 of 1965), section 7A(3), inserted by Act No. 73 of 1976, 8 October 1976";

    private const string GoaStatements = "Goa Labour Welfare Fund Rules, 1990, rule 4, made by the Department of Labour, Government of Goa under section 37 of the Goa, Daman and Diu Labour Welfare Fund Act, 1986 (Act 4 of 1987), Official Gazette, Series I, No. 49, 8 March 1990: “They shall come into force at once.”";

    private const string KeralaDueDays = "Kerala Labour Welfare Fund Act, 1975 (11 of 1977), section 15(2)";

    private const string HaryanaDefinitions = "Punjab Labour Welfare Fund Act, 1965 (Punjab Act 17 of 1965), section 2(2), (4) and (5) as in force in Haryana, clauses (2) and (4) as substituted by Haryana Act No. 7 of 2007, 4 April 2007";

    private const string DelhiDefinitions = "Bombay Labour Welfare Fund Act, 1953 (Bombay Act XL of 1953), section 2, as extended to the Union territory of Delhi with modifications by G.S.R. 1286(E), 15 December 1986";

    private const string MaharashtraDefinitions = "Maharashtra Labour Welfare Fund Act, 1953 (Mah. XL of 1953), section 2(2), (4) and (5), clause (2) as substituted by Mah. 24 of 2003 with effect from 7 January 2002";

    private const string GoaDefinitions = "Goa Labour Welfare Fund Act, 1986 (Act 4 of 1987), section 2(c) and (e), assented to by the President of India on 4 April 1987, Official Gazette, Series I, No. 5, 30 April 1987";

    private const string KeralaDefinitions = "Kerala Labour Welfare Fund Act, 1975 (11 of 1977), section 2(d) and (f), as amended by the Kerala Labour Welfare Fund (Amendment) Act, 1986 (Act 1 of 1987), deemed in force from 25 February 1984, and the Kerala Labour Welfare Fund (Amendment) Act, 1994 (Act 7 of 1994)";

    private const string TelanganaDefinitions = "Telangana Labour Welfare Fund Act, 1987 (Act No. 34 of 1987), section 2(2) and (4), as the Labour Department of Telangana publishes it";

    private static readonly DateOnly FirstDay = new(2026, 4, 1);

    private static readonly DateOnly LastDayOfHaryanasLimit = new(2026, 12, 31);

    private static readonly DateOnly HaryanasLimitIndexed = new(2027, 1, 1);

    private static readonly DateOnly Today = new(2026, 11, 8);

    [Theory]
    [InlineData("06-9A(1)", "06", WelfareFundPeriod.Monthly, null, "0.2", "35", null, 2, null, null, "an amount equal to zero point two percent of his salary or wages or any remuneration subject to a limit of rupees thirty-five", "section 9A(1) and (3) as in force in Haryana, sub-section (1) as substituted by Haryana Act No. 18 of 2019")]
    [InlineData("07-6BB(2)", "07", WelfareFundPeriod.HalfYearly, "0.75", null, null, "2.25", null, null, 2, "the amount of contribution payable every six months in respect of every employee shall be 75 paise … in respect of an employer for each such employee shall be 225 paise … (8) … an amount equal to twice the employees' contribution", "section 6BB(2) and (8), as extended to the Union territory of Delhi by G.S.R. 1286(E)")]
    [InlineData("21-6(1)", "21", WelfareFundPeriod.Yearly, "20", null, null, "40", null, null, null, "Every employee shall contribute twenty rupees per year to the Fund and every employer shall, in respect of each employee, contribute forty rupees per year to the Fund.", "Odisha Labour Welfare Fund Rules, 2015, rule 6(1)")]
    [InlineData("24-6(B)", "24", WelfareFundPeriod.HalfYearly, "6", null, null, "12", null, null, null, "દર છ મહિને એવા દરેક કામદાર દીઠ ૬ રૂપિયાના દરે અને માલિકના ફાળાની રકમ, દર છ મહિને એવા દરેક કામદાર દીઠ ૧૨ રૂપિયાના દરે", "Notification No. KHR-196-2016-KKB-10-2016-190722-M.4, Labour and Employment Department, Government of Gujarat, 10 November 2016")]
    [InlineData("27-6BB(2)", "27", WelfareFundPeriod.HalfYearly, "25", null, null, null, 3, null, 2, "shall be at the rate of twenty-five rupees", "section 6BB(2) as substituted by the Maharashtra Labour Welfare Fund (Amendment) Act, 2024")]
    [InlineData("29-7A(2)", "29", WelfareFundPeriod.Yearly, "50", null, null, "100", null, "50", null, "sum of fifty rupees, one hundred rupees and fifty rupees respectively shall be payable as contribution for that year by the concerned employee, the employer and the State Government", "sub-section (2) as amended by the Karnataka Labour Welfare Fund (Amendment) Act, 2024 (Karnataka Act No. 05 of 2025), section 2")]
    [InlineData("30-14(1)", "30", WelfareFundPeriod.Yearly, "120", null, null, "360", null, "240", null, "contribute rupees three hundred and sixty and rupees two hundred and forty, respectively, per year to the Fund", "section 14(1) as substituted by the Goa Labour Welfare Fund (Amendment) Act, 2015 (Goa Act 9 of 2015), section 6")]
    [InlineData("32-15(1)", "32", WelfareFundPeriod.HalfYearly, "45", null, null, "45", null, null, null, "Every employee shall contribute forty five rupees per half year to the Fund and every employer shall in respect of each such employee contribute forty five rupees per half year to the Fund.", "sub-section (1) as amended by the Kerala Labour Welfare Fund (Amendment) Act, 2021, section 2")]
    [InlineData("36-10(1)", "36", WelfareFundPeriod.Yearly, "2", null, null, "5", null, null, null, "Every employee shall contribute two rupees per year to the Fund and every employer shall, in respect of each such employee, contribute five rupees per year to the Fund", "Telangana Labour Welfare Fund Act, 1987 (Act No. 34 of 1987), section 10(1)")]
    public async Task Contribution_OfEachStateRead_IsTheShareItsInstrumentFixes(string entry, string stateCode, WelfareFundPeriod period, string? employeeAmount, string? employeePercent, string? employeeLimit, string? employerAmount, int? employerTimesEmployee, string? governmentAmount, int? governmentTimesEmployee, string words, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var contributions = (await StoreOf(scope).GetAsOfAsync<WelfareFundContributions>(FirstDay, TestContext.Current.CancellationToken))!.Contributions;

        var found = Assert.Single(contributions, candidate => candidate.Entry == entry);
        Assert.Equal((stateCode, period), (found.StateCode, found.Period));
        Assert.Equal((Rupees(employeeAmount), Percent(employeePercent), Rupees(employeeLimit)), (found.EmployeeAmount, found.EmployeePercent, found.EmployeeLimit));
        Assert.Equal((Rupees(employerAmount), employerTimesEmployee), (found.EmployerAmount, found.EmployerTimesEmployee));
        Assert.Equal((Rupees(governmentAmount), governmentTimesEmployee), (found.GovernmentAmount, found.GovernmentTimesEmployee));
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.Contains(instrument, await SourceAsync(scope, ContributionsKey, FirstDay, entry), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("06-9A(1)", "(3) The employer shall be entitled to recover from the employee the employee's contribution by deduction from his wages and not otherwise:")]
    [InlineData("07-6BB(2)", null)]
    [InlineData("21-6(1)", null)]
    [InlineData("24-6(B)", null)]
    [InlineData("27-6BB(2)", "nor shall it be made from any wages other than the wages for the months of June and December")]
    [InlineData("29-7A(2)", "nor shall it be made from any wages other than the wages for the month of December of the year to which the contribution relates")]
    [InlineData("30-14(1)", "such deduction shall be deemed to be a deduction authorised by or under the Payment of Wages Act, 1936 (Central Act 4 of 1936).")]
    [InlineData("32-15(1)", "by deduction from his wages or in such other manner as may be prescribed")]
    [InlineData("36-10(1)", null)]
    public async Task Deduction_OfEachStateRead_IsTheRecoveryFromWagesItsActAllows(string entry, string? words)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var contributions = (await StoreOf(scope).GetAsOfAsync<WelfareFundContributions>(FirstDay, TestContext.Current.CancellationToken))!.Contributions;

        AssertWords(words, Assert.Single(contributions, candidate => candidate.Entry == entry).Deduction);
    }

    [Fact]
    public async Task Contributions_OnTheFirstDay_HoldTheNineStatesReadAndNoOther()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await StoreOf(scope).GetAsOfAsync<WelfareFundContributions>(FirstDay, TestContext.Current.CancellationToken);

        Assert.NotNull(set);
        Assert.Equal((FirstDay, (DateOnly?)LastDayOfHaryanasLimit), (set.Version.From, set.Version.Until));
        Assert.Equal(["06", "07", "21", "24", "27", "29", "30", "32", "36"], set.Contributions.Select(contribution => contribution.StateCode).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Contribution_OfHaryana_IsInForceOnTheLastDayOfItsLimitAndGoneWhenTheLimitIsIndexed()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = StoreOf(scope);

        var before = await store.GetAsOfAsync<WelfareFundContributions>(LastDayOfHaryanasLimit, TestContext.Current.CancellationToken);
        var after = await store.GetAsOfAsync<WelfareFundContributions>(HaryanasLimitIndexed, TestContext.Current.CancellationToken);

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.Equal((FirstDay, (DateOnly?)LastDayOfHaryanasLimit), (before.Version.From, before.Version.Until));
        Assert.Equal((HaryanasLimitIndexed, (DateOnly?)null), (after.Version.From, after.Version.Until));
        Assert.Contains(before.Contributions, contribution => contribution.Entry == "06-9A(1)");
        Assert.Equal(before.Contributions.Where(contribution => contribution.StateCode != "06"), after.Contributions);
    }

    [Theory]
    [InlineData("06-9A(2)", "06", WelfareFundObligation.Payment, null, WelfareFundDueBound.Before, "12-31", null, "both his contribution and the contribution of the employee before the 31st December of every year through online mode", "section 9A(2) as in force in Haryana, substituted by Haryana Act No. 16 of 2021")]
    [InlineData("07-6BB(3).january", "07", WelfareFundObligation.Payment, null, WelfareFundDueBound.Before, "01-15", "12-31", "before the 15th day of July and 15th day of January", "the second of its two days, for the register of 31 December of section 6BB(2)")]
    [InlineData("07-6BB(3).july", "07", WelfareFundObligation.Payment, null, WelfareFundDueBound.Before, "07-15", "06-30", "before the 15th day of July and 15th day of January", "the first of its two days, for the register of 30 June of section 6BB(2)")]
    [InlineData("21-6(2).january", "21", WelfareFundObligation.Payment, "F", WelfareFundDueBound.Before, "01-15", null, "before the 15th day of January and the 15th day of July of every year in respect of all employees whose names stand on the register of establishment preceding the 31st December and the 30th June respectively, along with a statement showing full particulars in Form F.", "rule 6(2): the first of its two days, for the employees on the register preceding 31 December")]
    [InlineData("21-6(2).july", "21", WelfareFundObligation.Payment, "F", WelfareFundDueBound.Before, "07-15", null, "before the 15th day of January and the 15th day of July of every year in respect of all employees whose names stand on the register of establishment preceding the 31st December and the 30th June respectively, along with a statement showing full particulars in Form F.", "rule 6(2): the second of its two days, for the employees on the register preceding 30 June")]
    [InlineData("27-3-A.january", "27", WelfareFundObligation.Statement, "A-1", WelfareFundDueBound.OnOrBefore, "01-31", "12-31", "on or before 31 day of July and 31st day of January a statement of employer's contribution and employees' contribution", "rule 3-A, in the Maharashtra Labour Welfare Board's consolidated text: the second of its two days, for the register of 31 December")]
    [InlineData("27-3-A.july", "27", WelfareFundObligation.Statement, "A-1", WelfareFundDueBound.OnOrBefore, "07-31", "06-30", "on or before 31 day of July and 31st day of January a statement of employer's contribution and employees' contribution", "rule 3-A, in the Maharashtra Labour Welfare Board's consolidated text: the first of its two days, for the register of 30 June")]
    [InlineData("27-6BB(3).january", "27", WelfareFundObligation.Payment, null, WelfareFundDueBound.Before, "01-15", "12-31", "before the 15th day of July and 15th day of January, as the case may be", "section 6BB(3): the second of its two days, for the register of 31 December of section 6BB(2)(a)")]
    [InlineData("27-6BB(3).july", "27", WelfareFundObligation.Payment, null, WelfareFundDueBound.Before, "07-15", "06-30", "before the 15th day of July and 15th day of January, as the case may be", "section 6BB(3): the first of its two days, for the register of 30 June of section 6BB(2)(a)")]
    [InlineData("29-3-A(1)", "29", WelfareFundObligation.Statement, "D", WelfareFundDueBound.OnOrBefore, "01-15", null, "on or before 15th of January, of every year a statement in Form 'D' of the contributions by the employers and the employees paid during the preceding year", "Karnataka Labour Welfare Fund Rules, 1968, rule 3-A(1)")]
    [InlineData("29-7A(3)", "29", WelfareFundObligation.Payment, null, WelfareFundDueBound.OnOrBefore, "01-15", "12-31", "shall be paid by the employer to the Board on or before 15th January of the following year", "section 7A(3), in the Labour Department's booklet of 2025: for the register of 31 December of section 7A(2)")]
    [InlineData("30-4.january", "30", WelfareFundObligation.Statement, "A", WelfareFundDueBound.OnOrBefore, "01-31", "12-31", "on or before the 31st day of July and 31st of January a statement of employer's contribution and employees' contribution", "rule 4: the second of its two days, for the register of 31 December, the statement in Form A")]
    [InlineData("30-4.july", "30", WelfareFundObligation.Statement, "A", WelfareFundDueBound.OnOrBefore, "07-31", "06-30", "on or before the 31st day of July and 31st of January a statement of employer's contribution and employees' contribution", "rule 4: the first of its two days, for the register of 30 June, the statement in Form A")]
    [InlineData("32-15(2).january", "32", WelfareFundObligation.Payment, null, WelfareFundDueBound.Before, "01-15", null, "before the 15th day of July and 15th day of January of every year", "section 15(2), in the Labour Commissionerate's copy: the second of its two days")]
    [InlineData("32-15(2).july", "32", WelfareFundObligation.Payment, null, WelfareFundDueBound.Before, "07-15", null, "before the 15th day of July and 15th day of January of every year", "section 15(2), in the Labour Commissionerate's copy: the first of its two days")]
    public async Task DueDay_OfEachEntry_IsTheDayItsInstrumentSets(string entry, string stateCode, WelfareFundObligation obligation, string? form, WelfareFundDueBound bound, string day, string? registerDay, string words, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var days = (await StoreOf(scope).GetAsOfAsync<WelfareFundDueDays>(FirstDay, TestContext.Current.CancellationToken))!.Days;

        var found = Assert.Single(days, candidate => candidate.Entry == entry);
        Assert.Equal((stateCode, obligation, form, bound), (found.StateCode, found.Obligation, found.Form, found.Bound));
        Assert.Equal((DayMonth(day), OptionalDayMonth(registerDay)), (found.Day, found.RegisterDay));
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.Contains(instrument, await SourceAsync(scope, DueDaysKey, FirstDay, entry), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DueDays_OnTheFirstDay_HoldTheDaysOfTheSevenStatesWhoseDaysWereRead()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await StoreOf(scope).GetAsOfAsync<WelfareFundDueDays>(FirstDay, TestContext.Current.CancellationToken);

        Assert.NotNull(set);
        Assert.Equal((FirstDay, (DateOnly?)null, 15), (set.Version.From, set.Version.Until, set.Days.Count));
        Assert.Equal(["06", "07", "21", "27", "29", "30", "32"], set.Days.Select(day => day.StateCode).Distinct().Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("06-2", "06", "in which, on any day. Ten or more employees are employed or were employed during the preceding twelve months", "'employee' means any person employed, directly by or through any agency(including a contractor) with or without the knowledge of the principal employer", null, null, null, "section 2(2), (4) and (5) as in force in Haryana, clauses (2) and (4) as substituted by Haryana Act No. 7 of 2007")]
    [InlineData("07-2", "07", "Any establishment within the meaning of the delhi shops and establishments act, 1954 … which employs, or on any working day during the preceding twelve months, employed five or more persons", null, "being employed in a supervisory capacity draw as wages exceeding one thousand and six hundred rupees per Mensem", "1600", null, "section 2, as extended to the Union territory of Delhi by G.S.R. 1286(E)")]
    [InlineData("27-2", "27", "employed five or more persons including the establishments which have been granted exemption partly or wholly under the proviso to section 4 of that Act", "any person who is employed for hire or reward to do any work, skilled or unskilled, manual, clerical, supervisory or technical in an establishment", "draws wages exceeding three thousand five hundred rupees per mensem", "3500", null, "section 2(2), (4) and (5), clause (2) as substituted by Mah. 24 of 2003")]
    [InlineData("30-2", "30", "(iv) establishment as defined under sub-section (9) of section 2 of the Goa, Daman and Diu Shops and Establishments Act, 1973 (Act 13 of 1974)", "for a continuous period of 30 days during the period of 12 months", "draws wages exceeding one thousand and six hundred rupees per mensem", "1600", 30, "section 2(c) and (e), Official Gazette, Series I, No. 5, 30 April 1987")]
    [InlineData("32-2", "32", "which employs, or on any working day during the preceeding twelve months employed two or more persons", "for an aggregate period of not less than thirty days during the preceding twelve months", "(C) who is employed as an apprentice or on part-time basis", null, 30, "the provisos to clause (f)(iv), which the 1986 Act omitted and the copy still prints")]
    [InlineData("36-2", "36", "which employs or on any working day during the preceding twelve months employed twenty or more persons", "for a period of thirty days during the period of twelve months", "draws wages exceeding Rs. 1,600 (Rupees sixteen hundred) per mensum", "1600", 30, "section 2(2) and (4), as the Labour Department of Telangana publishes it")]
    public async Task Coverage_OfEachStateRead_IsWhoItsActCovers(string entry, string stateCode, string establishments, string? employees, string? excluded, string? supervisorWageLimit, int? minimumDaysEmployed, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var definitions = (await StoreOf(scope).GetAsOfAsync<WelfareFundCoverage>(FirstDay, TestContext.Current.CancellationToken))!.Definitions;

        var found = Assert.Single(definitions, candidate => candidate.Entry == entry);
        Assert.Equal((stateCode, Rupees(supervisorWageLimit), minimumDaysEmployed), (found.StateCode, found.SupervisorWageLimit, found.MinimumDaysEmployed));
        Assert.Contains(establishments, found.Establishments, StringComparison.Ordinal);
        AssertWords(employees, found.Employees);
        AssertWords(excluded, found.Excluded);
        Assert.Contains(instrument, await SourceAsync(scope, CoverageKey, FirstDay, entry), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Coverage_OfKerala_LeavesOutTheProvisosItsAmendingActOmitted()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var definitions = (await StoreOf(scope).GetAsOfAsync<WelfareFundCoverage>(FirstDay, TestContext.Current.CancellationToken))!.Definitions;

        var kerala = Assert.Single(definitions, definition => definition.StateCode == "32");
        Assert.Contains("employed two or more persons: … (v) an establishment", kerala.Establishments, StringComparison.Ordinal);
        Assert.DoesNotContain("Provided that any such commercial establishment shall continue", kerala.Establishments, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Coverage_OnTheFirstDay_HoldsTheSixStatesWhoseDefinitionsWereRead()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await StoreOf(scope).GetAsOfAsync<WelfareFundCoverage>(FirstDay, TestContext.Current.CancellationToken);

        Assert.NotNull(set);
        Assert.Equal((FirstDay, (DateOnly?)null), (set.Version.From, set.Version.Until));
        Assert.Equal(["06", "07", "27", "30", "32", "36"], set.Definitions.Select(definition => definition.StateCode).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(ContributionsKey, "06-9A(1)", "act", "Punjab Labour Welfare Fund Act, 1965, as amended in Haryana")]
    [InlineData(ContributionsKey, "07-6BB(2)", "act", "Bombay Labour Welfare Fund Act, 1953, as extended to the Union territory of Delhi")]
    [InlineData(ContributionsKey, "21-6(1)", "act", "Odisha Labour Welfare Fund Act, 2005")]
    [InlineData(ContributionsKey, "24-6(B)", "act", "Labour Welfare Fund Act, 1953, as in force in Gujarat")]
    [InlineData(ContributionsKey, "27-6BB(2)", "act", "Maharashtra Labour Welfare Fund Act, 1953")]
    [InlineData(ContributionsKey, "29-7A(2)", "act", "Karnataka Labour Welfare Fund Act, 1965")]
    [InlineData(ContributionsKey, "30-14(1)", "act", "Goa Labour Welfare Fund Act, 1986")]
    [InlineData(ContributionsKey, "32-15(1)", "act", "Kerala Labour Welfare Fund Act, 1975")]
    [InlineData(ContributionsKey, "36-10(1)", "act", "Telangana Labour Welfare Fund Act, 1987")]
    [InlineData(DueDaysKey, "06-9A(2)", "name", "Contributions of every month of the year")]
    [InlineData(DueDaysKey, "07-6BB(3).january", "name", "Contributions for the register of 31 December")]
    [InlineData(DueDaysKey, "07-6BB(3).july", "name", "Contributions for the register of 30 June")]
    [InlineData(DueDaysKey, "21-6(2).january", "name", "Contributions for the register preceding 31 December")]
    [InlineData(DueDaysKey, "21-6(2).july", "name", "Contributions for the register preceding 30 June")]
    [InlineData(DueDaysKey, "27-3-A.january", "name", "Statement for the register of 31 December")]
    [InlineData(DueDaysKey, "27-3-A.july", "name", "Statement for the register of 30 June")]
    [InlineData(DueDaysKey, "27-6BB(3).january", "name", "Contributions for the register of 31 December")]
    [InlineData(DueDaysKey, "27-6BB(3).july", "name", "Contributions for the register of 30 June")]
    [InlineData(DueDaysKey, "29-3-A(1)", "name", "Statement of the contributions paid in the year before")]
    [InlineData(DueDaysKey, "29-7A(3)", "name", "Contributions for the register of 31 December")]
    [InlineData(DueDaysKey, "30-4.january", "name", "Statement for the register of 31 December")]
    [InlineData(DueDaysKey, "30-4.july", "name", "Statement for the register of 30 June")]
    [InlineData(DueDaysKey, "32-15(2).january", "name", "Contributions due before 15 January")]
    [InlineData(DueDaysKey, "32-15(2).july", "name", "Contributions due before 15 July")]
    [InlineData(CoverageKey, "06-2", "act", "Punjab Labour Welfare Fund Act, 1965, as amended in Haryana")]
    [InlineData(CoverageKey, "07-2", "act", "Bombay Labour Welfare Fund Act, 1953, as extended to the Union territory of Delhi")]
    [InlineData(CoverageKey, "27-2", "act", "Maharashtra Labour Welfare Fund Act, 1953")]
    [InlineData(CoverageKey, "30-2", "act", "Goa Labour Welfare Fund Act, 1986")]
    [InlineData(CoverageKey, "32-2", "act", "Kerala Labour Welfare Fund Act, 1975")]
    [InlineData(CoverageKey, "36-2", "act", "Telangana Labour Welfare Fund Act, 1987")]
    public async Task Title_OfEachRow_IsTheActOrTheDueDayItNames(string key, string row, string column, string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var found = Assert.Single((await VersionAsync(scope, key, FirstDay)).Rows, candidate => candidate.Key == row);

        Assert.Equal(title, Assert.Single(found.Cells, cell => cell.Column == column).Value);
    }

    [Theory]
    [InlineData(ContributionsKey, "2026-04-01,2027-01-01")]
    [InlineData(DueDaysKey, "2026-04-01")]
    [InlineData(CoverageKey, "2026-04-01")]
    public async Task Versions_OfEachSet_AreTheDaysItsInstrumentsGovernAndTheLawByTheLastDayOfTheContributionPeriod(string key, string dates)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, Today, TestContext.Current.CancellationToken);

        Assert.NotNull(set);
        Assert.Equal((ParameterSetBasis.Law, ParameterAsOf.ContributionPeriodEnd), (set.Basis, set.AsOf));
        Assert.Equal(dates.Split(',').Select(Day).Reverse(), set.Versions.Select(version => version.From));
    }

    [Theory]
    [InlineData(ContributionsKey, "2026-04-01", "2026-12-31", 9, 0, 0, new[] { HaryanaContributions, DelhiContributions, DelhiFund, OdishaContributions, GujaratContributions, MaharashtraContributions, MaharashtraAmendment, KarnatakaContributions, KarnatakaAmendment, GoaDeduction, GoaAmendment, KeralaContributions, KeralaAmendment, TelanganaContributions })]
    [InlineData(ContributionsKey, "2027-01-01", null, 0, 0, 1, new[] { HaryanaIndexing })]
    [InlineData(DueDaysKey, "2026-04-01", null, 15, 0, 0, new[] { HaryanaDueDay, DelhiDueDays, OdishaDueDays, MaharashtraStatements, MaharashtraDueDays, KarnatakaStatement, KarnatakaDueDay, GoaStatements, KeralaDueDays })]
    [InlineData(CoverageKey, "2026-04-01", null, 6, 0, 0, new[] { HaryanaDefinitions, DelhiDefinitions, MaharashtraDefinitions, GoaDefinitions, KeralaDefinitions, TelanganaDefinitions })]
    public async Task Version_OfEachSet_CitesItsInstrumentsInOrderAndChangesTheRowsTheyChange(string key, string from, string? until, int added, int changed, int removed, string[] instruments)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, Today, TestContext.Current.CancellationToken);

        var version = Assert.Single(set!.Versions, candidate => candidate.From == Day(from));
        Assert.Equal(OptionalDay(until), version.Until);
        Assert.Equal(instruments, version.Instruments);
        Assert.Equal((added, changed, removed), (version.AddedCount, version.ChangedCount, version.RemovedCount));
    }

    [Theory]
    [InlineData(ContributionsKey, "A State without a row is one whose instrument the ERP has not read, never a finding that it has no fund.")]
    [InlineData(ContributionsKey, "Haryana's limit is indexed each 1 January, so its row ends on 31 December 2026 until the limit for 2027 is seeded.")]
    [InlineData(ContributionsKey, "Goa and Odisha fix yearly amounts, with two days a year in labour-welfare-fund.due-days, and state no split of the amount between them.")]
    [InlineData(DueDaysKey, "A State without a row is one whose days the ERP has not read, never a finding that nothing falls due.")]
    [InlineData(DueDaysKey, "Goa's rules set days for the half-yearly statements and none for paying")]
    [InlineData(CoverageKey, "A State without a row is one whose definitions the ERP has not read in full, never a finding that its fund covers no one")]
    public async Task Description_OfEachSet_SaysWhatAMissingRowOrDayMeans(string key, string words)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, Today, TestContext.Current.CancellationToken);

        Assert.Contains(words, set!.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DueDaysAndCoverage_WhenHaryanasLimitIsIndexed_AreStillTheFirstVersionWithNoEnd()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = StoreOf(scope);

        var days = await store.GetAsOfAsync<WelfareFundDueDays>(HaryanasLimitIndexed, TestContext.Current.CancellationToken);
        var coverage = await store.GetAsOfAsync<WelfareFundCoverage>(HaryanasLimitIndexed, TestContext.Current.CancellationToken);

        Assert.Equal((FirstDay, (DateOnly?)null, 15), (days!.Version.From, days.Version.Until, days.Days.Count));
        Assert.Equal((FirstDay, (DateOnly?)null, 6), (coverage!.Version.From, coverage.Version.Until, coverage.Definitions.Count));
    }

    [Fact]
    public async Task Sets_OnTheDayBeforeTheWindowOpens_HoldNothing()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = StoreOf(scope);
        var day = FirstDay.AddDays(-1);

        Assert.Null(await store.GetAsOfAsync<WelfareFundContributions>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<WelfareFundDueDays>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<WelfareFundCoverage>(day, TestContext.Current.CancellationToken));
    }

    private static IStatutoryParameters StoreOf(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

    private static DateOnly Day(string value) => DateOnly.Parse(value, CultureInfo.InvariantCulture);

    private static DateOnly? OptionalDay(string? value) => value is null ? null : Day(value);

    private static decimal Number(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private static Money? Rupees(string? value) => value is null ? null : new Money(Number(value), Currency.Inr);

    private static Percentage? Percent(string? value) => value is null ? null : new Percentage(Number(value));

    private static DayAndMonth DayMonth(string value) =>
        DayAndMonth.TryParse(value, out var day) ? day : throw new ArgumentException($"'{value}' is not a day and month written MM-dd.", nameof(value));

    private static DayAndMonth? OptionalDayMonth(string? value) => value is null ? null : DayMonth(value);

    private static void AssertWords(string? expected, string? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
        }
        else
        {
            Assert.Contains(expected, actual, StringComparison.Ordinal);
        }
    }

    private static async Task<ParameterVersionView> VersionAsync(AsyncServiceScope scope, string key, DateOnly day)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, day, TestContext.Current.CancellationToken);

        Assert.NotNull(view);
        return view;
    }

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string key, DateOnly day, string rowKey) =>
        Assert.Single((await VersionAsync(scope, key, day)).Rows, row => row.Key == rowKey).Source;
}
