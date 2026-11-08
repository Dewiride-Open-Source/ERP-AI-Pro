using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Esi;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Labour;

// Golden values of the Employees' State Insurance from 1 April 2026: the contribution rates of rule 19 of the Social Security
// (Central) Rules, 2026, the contributions not paid, the divisors of the average daily wages, the coverage wage limits, the
// contribution and benefit periods and the due dates, each pinned beside its instrument so that a wrong edit of a seed file fails a
// test (ADR-0063). Each case is a day at the edge of a version: the Rules of 2026 superseded those of 1950 on 8 May 2026, and the
// rows of the Regulations of 1950 end on 20 November 2026, the earlier day on which the one year that section 164(2)(b) of the
// Code on Social Security, 2020 keeps them in force may end.
[Collection(StatutoryRows.Name)]
public sealed class EsiGoldenTests(ErpApiFactory factory)
{
    private const string RatesKey = "esi.contribution-rates";

    private const string ExemptionsKey = "esi.contribution-exemptions";

    private const string DivisorsKey = "esi.average-daily-wage-divisors";

    private const string CoverageKey = "esi.coverage-wage-limits";

    private const string PeriodsKey = "esi.contribution-periods";

    private const string DueDatesKey = "esi.contribution-due-dates";

    private const string Rules1950 = "Employees' State Insurance (Central) Rules, 1950, ";

    private const string Regulations1950 = "Employees' State Insurance (General) Regulations, 1950, ";

    private const string Rules2026 = "Social Security (Central) Rules, 2026, ";

    private const string ConsolidatedRules = Rules1950 + "S.R.O. 212, 22 June 1950, Ministry of Labour, in ESIC's consolidated text as on 26.04.2024: ";

    private const string ConsolidatedRegulations = Regulations1950 + "Employees' State Insurance Corporation Notification No. RS/5/48, 17 October 1950, in ESIC's consolidated text as on 11.01.2024, as amended by the ESI (General) (Amdt.) Regulations, 2020 of 18 May 2020: ";

    private const string NotificationOfRules2026 = Rules2026 + "G.S.R. 344(E), 8 May 2026, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (i), No. 310, 8 May 2026, ";

    private const string Supersession = "made “in supersession of the— … (iii) Employees' State Insurance (Central) Rules, 1950”";

    private const string Code = "Code on Social Security, 2020 (36 of 2020), Gazette of India, Extraordinary, Part II, Section 1, No. 61, 29 September 2020: ";

    private const string KeptInForce = "section 164(2)(a) and (b), which keep in force the rules and regulations made under the Employees' State Insurance Act, 1948, repealed by section 164(1)";

    private const string Continuation = Code + KeptInForce;

    private const string Commencement = "S.O. 5319(E), 21 November 2025, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (ii), No. 5143, as corrected by S.O. 5936(E), 19 December 2025, No. 5739 of 20 December 2025: ";

    private const string ExceptedAndInForce = " “except the provisions of the Code specified” in S.O. 2060(E), dated the 3rd May, 2023, in force from 21 November 2025";

    private const string CommencementOfSection164 = Commencement + "section 164" + ExceptedAndInForce;

    private const string YearOfSection164 = Code + "section 164(2)(b): the rules and regulations made under the Employees' State Insurance Act, 1948 “shall remain in force, to the extent they are not inconsistent with the provisions of this Code for a period of one year from the date of commencement of this Code”";

    private const string ReferenceToCommencement = Code + "section 1(3): “any reference in any such provision to the commencement of this Code shall be construed as a reference to the coming into force of that provision”";

    private const string TimeRate = "the amount of wage which would have been payable to him for the complete wage period had he worked on all the working days in that wage period, divided by 26 if he is monthly rated, 13 if he is fortnightly rated, 6 if he is weekly rated and 1 if he is daily rated;";

    private const string OtherBasis = "the amount of wages earned during the complete wage period in the contribution period divided by the number of days in full or part for which he has worked for wages in that wage period:";

    private const string NoDayWorked1950 = "he shall be deemed to have worked for 26, 13, 6 or 1 days or day if the wage period be a month, a fortnight, a week or a day respectively.";

    private const string NoDayWorked2026 = "he shall be deemed to have worked for 26, 13, 6 or 1 day or days if the wage period be a month, a fortnight, a week or a day respectively.";

    private const string Rule51A = "the employer shall not be required to pay employer's share of contribution upto a maximum period of three years from the date of commencement of the contribution period. (2) The employer's share of contribution in respect of such employees and for such period under sub-section (1) shall be reimbursed to the Corporation by the Central Government.";

    private const string Rule19Sub2 = "the employer shall not be required to pay employer's share of contribution up to a maximum period of three years or for such period as may be specified by the Central Government, from the date of commencement of the contribution period. (3) The employer's share of contribution in respect of such employees and for such period under sub-rule (2) shall be reimbursed to the Corporation by the Central Government.";

    private const string Regulation4 = "4. Contribution and Benefit periods. Contribution periods and the corresponding benefit periods shall be as under:— ";

    private const string Regulation31 = "31. Time for payment of contribution. An employer who is liable to pay contributions in respect of any employee shall pay those contributions within 15 days of the last day of the calendar month in which the contributions fall due:";

    private static readonly DateOnly FirstDay = new(2026, 4, 1);

    private static readonly DateOnly Rules2026InForce = new(2026, 5, 8);

    private static readonly DateOnly Today = new(2026, 11, 8);

    [Theory]
    [InlineData("employer", "Employer's contribution", "3.25", "(a) employer's contribution, a sum (rounded to the next higher rupee) equal to three and one-fourth per cent. of the wages payable to an employee; and", Rules2026 + "rule 19(1)(a)")]
    [InlineData("employee", "Employee's contribution", "0.75", "(b) employee's contribution, a sum (rounded to the next higher rupee) equal to three-fourth per cent. of the wages payable to an employee.", Rules2026 + "rule 19(1)(b)")]
    public async Task Rate_OfEachShareFromTheDayRule19CameIntoForce_IsItsShareOfTheWagesRoundedUpToTheRupee(string share, string name, string rate, string words, string source)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var rates = (await StoreOf(scope).GetAsOfAsync<EsiContributionRates>(Rules2026InForce, TestContext.Current.CancellationToken))!.Rates;

        var found = Assert.Single(rates, candidate => candidate.Share == share);
        Assert.Equal((name, new Percentage(Number(rate)), new Money(1m, Currency.Inr)), (found.Name, found.Rate, found.RoundedUpTo));
        Assert.Equal($"19. Rate of contributions.—(1) The amount of contribution for a wage period shall be in respect of— {words}", found.Wording);
        Assert.Equal(source, await SourceAsync(scope, RatesKey, Rules2026InForce, share));
    }

    [Theory]
    [InlineData("2026-04-01")]
    [InlineData("2026-05-07")]
    public async Task Rates_BeforeRule19CameIntoForce_HoldNothing(string day)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        Assert.Null(await StoreOf(scope).GetAsOfAsync<EsiContributionRates>(Day(day), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("2026-04-01", "employee-share-average-daily-wages", "176", null, "52. Exemption from payment of employee's contribution. The average daily wages during a wage period for exemption from payment of employee's contribution under section 42 shall be upto and inclusive of rupees one hundred seventy-six only.", Rules1950 + "rule 52, in ESIC's consolidated text as on 26.04.2024")]
    [InlineData("2026-05-07", "employee-share-average-daily-wages", "176", null, "52. Exemption from payment of employee's contribution. The average daily wages during a wage period for exemption from payment of employee's contribution under section 42 shall be upto and inclusive of rupees one hundred seventy-six only.", Rules1950 + "rule 52, in ESIC's consolidated text as on 26.04.2024")]
    [InlineData("2026-05-08", "employee-share-average-daily-wages", null, null, null, NotificationOfRules2026 + Supersession)]
    [InlineData("2026-04-01", "employer-share-disability", null, 3, Rule51A, Rules1950 + "rule 51A, in ESIC's consolidated text as on 26.04.2024")]
    [InlineData("2026-05-07", "employer-share-disability", null, 3, Rule51A, Rules1950 + "rule 51A, in ESIC's consolidated text as on 26.04.2024")]
    [InlineData("2026-05-08", "employer-share-disability", null, 3, Rule19Sub2, Rules2026 + "rule 19(2) and (3)")]
    public async Task Exemption_OnADayAtTheEdgeOfAVersion_IsAsItsInstrumentWritesIt(string day, string exemption, string? averageDailyWagesUpTo, int? years, string? words, string instrument)
    {
        var date = Day(day);
        await using var scope = factory.Services.CreateAsyncScope();

        var exemptions = (await StoreOf(scope).GetAsOfAsync<EsiContributionExemptions>(date, TestContext.Current.CancellationToken))!.Exemptions;
        var version = await VersionAsync(scope, ExemptionsKey, date);

        var found = exemptions.SingleOrDefault(candidate => candidate.Exemption == exemption);
        if (words is null)
        {
            Assert.Null(found);
            Assert.Contains(version.Instruments, cited => cited.StartsWith(instrument, StringComparison.Ordinal));
            return;
        }

        Assert.NotNull(found);
        Assert.Equal((Rupees(averageDailyWagesUpTo), years), (found.AverageDailyWagesUpTo, found.Years));
        Assert.EndsWith(words, found.Wording, StringComparison.Ordinal);
        Assert.Equal(instrument, Assert.Single(version.Rows, row => row.Key == exemption).Source);
    }

    [Theory]
    [InlineData("2026-04-01", "monthly-rated", 26, TimeRate, Rules1950 + "rule 2(1B)(a)")]
    [InlineData("2026-04-01", "fortnightly-rated", 13, TimeRate, Rules1950 + "rule 2(1B)(a)")]
    [InlineData("2026-04-01", "weekly-rated", 6, TimeRate, Rules1950 + "rule 2(1B)(a)")]
    [InlineData("2026-04-01", "daily-rated", 1, TimeRate, Rules1950 + "rule 2(1B)(a)")]
    [InlineData("2026-04-01", "other-basis", null, OtherBasis, Rules1950 + "rule 2(1B)(b), the proviso and the Explanation")]
    [InlineData("2026-04-01", "no-day-worked-month", 26, NoDayWorked1950, Rules1950 + "rule 2(1B), proviso")]
    [InlineData("2026-04-01", "no-day-worked-fortnight", 13, NoDayWorked1950, Rules1950 + "rule 2(1B), proviso")]
    [InlineData("2026-04-01", "no-day-worked-week", 6, NoDayWorked1950, Rules1950 + "rule 2(1B), proviso")]
    [InlineData("2026-04-01", "no-day-worked-day", 1, NoDayWorked1950, Rules1950 + "rule 2(1B), proviso")]
    [InlineData("2026-05-07", "monthly-rated", 26, TimeRate, Rules1950 + "rule 2(1B)(a)")]
    [InlineData("2026-05-07", "fortnightly-rated", 13, TimeRate, Rules1950 + "rule 2(1B)(a)")]
    [InlineData("2026-05-07", "weekly-rated", 6, TimeRate, Rules1950 + "rule 2(1B)(a)")]
    [InlineData("2026-05-07", "daily-rated", 1, TimeRate, Rules1950 + "rule 2(1B)(a)")]
    [InlineData("2026-05-07", "other-basis", null, OtherBasis, Rules1950 + "rule 2(1B)(b), the proviso and the Explanation")]
    [InlineData("2026-05-07", "no-day-worked-month", 26, NoDayWorked1950, Rules1950 + "rule 2(1B), proviso")]
    [InlineData("2026-05-07", "no-day-worked-fortnight", 13, NoDayWorked1950, Rules1950 + "rule 2(1B), proviso")]
    [InlineData("2026-05-07", "no-day-worked-week", 6, NoDayWorked1950, Rules1950 + "rule 2(1B), proviso")]
    [InlineData("2026-05-07", "no-day-worked-day", 1, NoDayWorked1950, Rules1950 + "rule 2(1B), proviso")]
    [InlineData("2026-05-08", "monthly-rated", 26, TimeRate, Rules2026 + "rule 2(1)(h)(i)")]
    [InlineData("2026-05-08", "fortnightly-rated", 13, TimeRate, Rules2026 + "rule 2(1)(h)(i)")]
    [InlineData("2026-05-08", "weekly-rated", 6, TimeRate, Rules2026 + "rule 2(1)(h)(i)")]
    [InlineData("2026-05-08", "daily-rated", 1, TimeRate, Rules2026 + "rule 2(1)(h)(i)")]
    [InlineData("2026-05-08", "other-basis", null, OtherBasis, Rules2026 + "rule 2(1)(h)(ii), the proviso and the Explanation")]
    [InlineData("2026-05-08", "no-day-worked-month", 26, NoDayWorked2026, Rules2026 + "rule 2(1)(h), proviso")]
    [InlineData("2026-05-08", "no-day-worked-fortnight", 13, NoDayWorked2026, Rules2026 + "rule 2(1)(h), proviso")]
    [InlineData("2026-05-08", "no-day-worked-week", 6, NoDayWorked2026, Rules2026 + "rule 2(1)(h), proviso")]
    [InlineData("2026-05-08", "no-day-worked-day", 1, NoDayWorked2026, Rules2026 + "rule 2(1)(h), proviso")]
    public async Task Divisor_OnADayAtTheEdgeOfAVersion_IsAsItsInstrumentWritesIt(string day, string divisor, int? days, string words, string source)
    {
        var date = Day(day);
        await using var scope = factory.Services.CreateAsyncScope();

        var divisors = (await StoreOf(scope).GetAsOfAsync<EsiAverageDailyWageDivisors>(date, TestContext.Current.CancellationToken))!.Divisors;

        var found = Assert.Single(divisors, candidate => candidate.Divisor == divisor);
        Assert.Equal(days, found.Days);
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.StartsWith(source, await SourceAsync(scope, DivisorsKey, date, divisor), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-04-01", "employee", "21000", "shall be twenty-one thousand rupees a month:", Rules1950 + "rule 50, in ESIC's consolidated text as on 26.04.2024")]
    [InlineData("2026-05-07", "employee", "21000", "shall be twenty-one thousand rupees a month:", Rules1950 + "rule 50, in ESIC's consolidated text as on 26.04.2024")]
    [InlineData("2026-05-08", "employee", null, null, NotificationOfRules2026 + Supersession)]
    [InlineData("2026-04-01", "employee-with-disability", "25000", "shall be twenty-five thousand rupees per month.", Rules1950 + "rule 50, second proviso, in ESIC's consolidated text as on 26.04.2024")]
    [InlineData("2026-05-07", "employee-with-disability", "25000", "shall be twenty-five thousand rupees per month.", Rules1950 + "rule 50, second proviso, in ESIC's consolidated text as on 26.04.2024")]
    [InlineData("2026-05-08", "employee-with-disability", null, null, NotificationOfRules2026 + Supersession)]
    [InlineData("2026-04-01", "continued-coverage", null, "exceed twenty-one thousand rupees a month at any time after and not before the beginning of the contribution period, shall continue to be an employee until the end of that period.", Rules1950 + "rule 50, first proviso, in ESIC's consolidated text as on 26.04.2024")]
    [InlineData("2026-05-07", "continued-coverage", null, "exceed twenty-one thousand rupees a month at any time after and not before the beginning of the contribution period, shall continue to be an employee until the end of that period.", Rules1950 + "rule 50, first proviso, in ESIC's consolidated text as on 26.04.2024")]
    [InlineData("2026-05-08", "continued-coverage", null, "the wage ceiling as may be notified by the Central Government under clause (89) of section 2 of the Code for the purposes of becoming a member under Chapter IV of the Code, shall continue to be an employee for the remainder of that contribution period.", "S.O. 2351(E), 8 May 2026, under the first proviso to section 2(26) of the Code on Social Security, 2020")]
    public async Task CoverageLimit_OnADayAtTheEdgeOfAVersion_IsAsItsInstrumentWritesIt(string day, string limit, string? amount, string? words, string instrument)
    {
        var date = Day(day);
        await using var scope = factory.Services.CreateAsyncScope();

        var limits = (await StoreOf(scope).GetAsOfAsync<EsiCoverageWageLimits>(date, TestContext.Current.CancellationToken))!.Limits;
        var version = await VersionAsync(scope, CoverageKey, date);

        var found = limits.SingleOrDefault(candidate => candidate.Limit == limit);
        if (words is null)
        {
            Assert.Null(found);
            Assert.Contains(version.Instruments, cited => cited.StartsWith(instrument, StringComparison.Ordinal));
            return;
        }

        Assert.NotNull(found);
        Assert.Equal(Rupees(amount), found.Amount);
        Assert.EndsWith(words, found.Wording, StringComparison.Ordinal);
        Assert.Equal(instrument, Assert.Single(version.Rows, row => row.Key == limit).Source);
    }

    [Theory]
    [InlineData("2026-04-01", "april-to-september", "04-01", "09-30", "01-01", "06-30", null, Regulation4 + "[Contribution period] 1st April to 30th September [Corresponding benefit period] 1st January of the year following, to 30th June.", Regulations1950 + "regulation 4, Table, in ESIC's consolidated text as on 11.01.2024")]
    [InlineData("2026-11-20", "april-to-september", "04-01", "09-30", "01-01", "06-30", null, Regulation4 + "[Contribution period] 1st April to 30th September [Corresponding benefit period] 1st January of the year following, to 30th June.", Regulations1950 + "regulation 4, Table, in ESIC's consolidated text as on 11.01.2024")]
    [InlineData("2026-11-21", "april-to-september", null, null, null, null, null, null, YearOfSection164)]
    [InlineData("2026-04-01", "october-to-march", "10-01", "03-31", "07-01", "12-31", null, Regulation4 + "[Contribution period] 1st October to 31st March of the year following [Corresponding benefit period] 1st July to 31st December.", Regulations1950 + "regulation 4, Table, in ESIC's consolidated text as on 11.01.2024")]
    [InlineData("2026-11-20", "october-to-march", "10-01", "03-31", "07-01", "12-31", null, Regulation4 + "[Contribution period] 1st October to 31st March of the year following [Corresponding benefit period] 1st July to 31st December.", Regulations1950 + "regulation 4, Table, in ESIC's consolidated text as on 11.01.2024")]
    [InlineData("2026-11-21", "october-to-march", null, null, null, null, null, null, YearOfSection164)]
    [InlineData("2026-04-01", "first-employment", null, null, null, null, 9, "Provided that in the case of a person who becomes an employee within the meaning of the Act for the first time, the first contribution period shall commence from the date of such employment in the contribution period current on that day and the corresponding benefit period for him shall commence on the expiry of the period of 9 months from the date of such employment.", Regulations1950 + "regulation 4, proviso, in ESIC's consolidated text as on 11.01.2024")]
    [InlineData("2026-11-20", "first-employment", null, null, null, null, 9, "Provided that in the case of a person who becomes an employee within the meaning of the Act for the first time, the first contribution period shall commence from the date of such employment in the contribution period current on that day and the corresponding benefit period for him shall commence on the expiry of the period of 9 months from the date of such employment.", Regulations1950 + "regulation 4, proviso, in ESIC's consolidated text as on 11.01.2024")]
    [InlineData("2026-11-21", "first-employment", null, null, null, null, null, null, YearOfSection164)]
    public async Task Period_OnADayAtTheEdgeOfAVersion_IsAsTheRegulationsWriteIt(string day, string period, string? startsOn, string? endsOn, string? benefitStartsOn, string? benefitEndsOn, int? benefitAfterMonths, string? words, string instrument)
    {
        var date = Day(day);
        await using var scope = factory.Services.CreateAsyncScope();

        var periods = (await StoreOf(scope).GetAsOfAsync<EsiContributionPeriods>(date, TestContext.Current.CancellationToken))!.Periods;
        var version = await VersionAsync(scope, PeriodsKey, date);

        var found = periods.SingleOrDefault(candidate => candidate.Period == period);
        if (words is null)
        {
            Assert.Null(found);
            Assert.Contains(version.Instruments, cited => cited.StartsWith(instrument, StringComparison.Ordinal));
            return;
        }

        Assert.NotNull(found);
        Assert.Equal(
            (DayMonth(startsOn), DayMonth(endsOn), DayMonth(benefitStartsOn), DayMonth(benefitEndsOn), benefitAfterMonths),
            (found.StartsOn, found.EndsOn, found.BenefitStartsOn, found.BenefitEndsOn, found.BenefitAfterMonths));
        Assert.Equal(words, found.Wording);
        Assert.Equal(instrument, Assert.Single(version.Rows, row => row.Key == period).Source);
    }

    [Theory]
    [InlineData("2026-04-01", "falls-due", null, "(4) The contributions payable in respect of each wage period shall ordinarily fall due on the last day of the wage period,", "Code on Social Security, 2020, section 29(4)")]
    [InlineData("2026-11-20", "falls-due", null, "(4) The contributions payable in respect of each wage period shall ordinarily fall due on the last day of the wage period,", "Code on Social Security, 2020, section 29(4)")]
    [InlineData("2026-11-21", "falls-due", null, "(4) The contributions payable in respect of each wage period shall ordinarily fall due on the last day of the wage period,", "Code on Social Security, 2020, section 29(4)")]
    [InlineData("2026-04-01", "part-period", null, "36. Employment for part of a wage period. Where an employee is employed by an employer for part of a wage period, the contributions in respect of such wage period, shall fall due on the last day of the employment by such employer in that wage period.", Regulations1950 + "regulation 36, in ESIC's consolidated text as on 11.01.2024")]
    [InlineData("2026-11-20", "part-period", null, "36. Employment for part of a wage period. Where an employee is employed by an employer for part of a wage period, the contributions in respect of such wage period, shall fall due on the last day of the employment by such employer in that wage period.", Regulations1950 + "regulation 36, in ESIC's consolidated text as on 11.01.2024")]
    [InlineData("2026-11-21", "part-period", null, null, YearOfSection164)]
    [InlineData("2026-04-01", "payment", 15, Regulation31, Regulations1950 + "regulation 31, in ESIC's consolidated text as on 11.01.2024")]
    [InlineData("2026-11-20", "payment", 15, Regulation31, Regulations1950 + "regulation 31, in ESIC's consolidated text as on 11.01.2024")]
    [InlineData("2026-11-21", "payment", null, null, YearOfSection164)]
    [InlineData("2026-04-01", "closure", null, Regulation31 + " Provided that where a factory/establishment is permanently closed, the employer shall pay contribution on the last day of its closure:", Regulations1950 + "regulation 31, first proviso, in ESIC's consolidated text as on 11.01.2024")]
    [InlineData("2026-11-20", "closure", null, Regulation31 + " Provided that where a factory/establishment is permanently closed, the employer shall pay contribution on the last day of its closure:", Regulations1950 + "regulation 31, first proviso, in ESIC's consolidated text as on 11.01.2024")]
    [InlineData("2026-11-21", "closure", null, null, YearOfSection164)]
    public async Task DueDate_OnADayAtTheEdgeOfAVersion_IsAsItsInstrumentWritesIt(string day, string rule, int? daysAfterMonthEnd, string? words, string instrument)
    {
        var date = Day(day);
        await using var scope = factory.Services.CreateAsyncScope();

        var rules = (await StoreOf(scope).GetAsOfAsync<EsiContributionDueDates>(date, TestContext.Current.CancellationToken))!.Rules;
        var version = await VersionAsync(scope, DueDatesKey, date);

        var found = rules.SingleOrDefault(candidate => candidate.Rule == rule);
        if (words is null)
        {
            Assert.Null(found);
            Assert.Contains(version.Instruments, cited => cited.StartsWith(instrument, StringComparison.Ordinal));
            return;
        }

        Assert.NotNull(found);
        Assert.Equal(daysAfterMonthEnd, found.DaysAfterMonthEnd);
        Assert.StartsWith(words, found.Wording, StringComparison.Ordinal);
        Assert.Equal(instrument, Assert.Single(version.Rows, row => row.Key == rule).Source);
    }

    [Theory]
    [InlineData(RatesKey, "2026-05-08")]
    [InlineData(ExemptionsKey, "2026-04-01,2026-05-08")]
    [InlineData(DivisorsKey, "2026-04-01,2026-05-08")]
    [InlineData(CoverageKey, "2026-04-01,2026-05-08")]
    [InlineData(PeriodsKey, "2026-04-01,2026-11-21")]
    [InlineData(DueDatesKey, "2026-04-01,2026-11-21")]
    public async Task Versions_OfEachSet_AreTheDatesItsInstrumentsGovernAndTheLawByTheLastDayOfTheWagePeriod(string key, string dates)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, Today, TestContext.Current.CancellationToken);

        Assert.NotNull(set);
        Assert.Equal((ParameterSetBasis.Law, ParameterAsOf.WagePeriodEnd), (set.Basis, set.AsOf));
        Assert.Equal(dates.Split(',').Select(Day).Reverse(), set.Versions.Select(version => version.From));
    }

    [Theory]
    [InlineData(RatesKey, "2026-05-08", 2, 0, 0, new[] { NotificationOfRules2026 + "rule 19(1); rule 1(2): “They shall come into force on the date of their publication in the Official Gazette.”" })]
    [InlineData(ExemptionsKey, "2026-04-01", 2, 0, 0, new[] { ConsolidatedRules + "rule 51A, inserted by G.S.R. 254(E), 31 March 2008, w.e.f. 1 April 2008, and renumbered by G.S.R. 959(E), 6 October 2016; rule 52, its amount substituted by G.S.R. 638(E), 6 September 2019, w.e.f. 6 September 2019", Continuation, CommencementOfSection164 })]
    [InlineData(ExemptionsKey, "2026-05-08", 0, 1, 1, new[] { NotificationOfRules2026 + Supersession + ", rule 19(2) and (3); rule 1(2)" })]
    [InlineData(DivisorsKey, "2026-04-01", 9, 0, 0, new[] { ConsolidatedRules + "rule 2(1B), inserted by G.S.R. 76, 22 January 1991, w.e.f. 1 February 1991", Continuation, CommencementOfSection164 })]
    [InlineData(DivisorsKey, "2026-05-08", 0, 9, 0, new[] { NotificationOfRules2026 + Supersession + ", rule 2(1)(h); rule 1(2)" })]
    [InlineData(CoverageKey, "2026-04-01", 3, 0, 0, new[] { ConsolidatedRules + "rule 50, inserted by G.S.R. 76, 22 January 1991, w.e.f. 1 February 1991, its limit substituted by G.S.R. 1166(E), 22 December 2016, w.e.f. 1 January 2017, and its second proviso inserted by G.S.R. 254(E), 31 March 2008, w.e.f. 1 April 2008", Continuation, CommencementOfSection164 })]
    [InlineData(CoverageKey, "2026-05-08", 0, 1, 2, new[] { NotificationOfRules2026 + Supersession + "; rule 1(2)", "S.O. 2351(E), 8 May 2026, Ministry of Labour and Employment, under the first proviso to section 2(26) of the Code on Social Security, 2020, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (ii), No. 2260, 8 May 2026, without words of commencement" })]
    [InlineData(PeriodsKey, "2026-04-01", 3, 0, 0, new[] { ConsolidatedRegulations + "regulation 4", Continuation, CommencementOfSection164 })]
    [InlineData(PeriodsKey, "2026-11-21", 0, 0, 3, new[] { YearOfSection164, ReferenceToCommencement, CommencementOfSection164 })]
    [InlineData(DueDatesKey, "2026-04-01", 4, 0, 0, new[] { ConsolidatedRegulations + "regulations 31 and 36", Code + "section 29(4), and " + KeptInForce, Commencement + "section 29, and section 164" + ExceptedAndInForce })]
    [InlineData(DueDatesKey, "2026-11-21", 0, 0, 3, new[] { YearOfSection164, ReferenceToCommencement, CommencementOfSection164 })]
    public async Task Version_OfEachSet_CitesItsInstrumentsInOrderAndChangesTheRowsTheyChange(string key, string from, int added, int changed, int removed, string[] instruments)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, Today, TestContext.Current.CancellationToken);

        var version = Assert.Single(set!.Versions, candidate => candidate.From == Day(from));
        Assert.Equal(instruments.Length, version.Instruments.Count);
        Assert.All(instruments.Zip(version.Instruments), pair => Assert.StartsWith(pair.First, pair.Second, StringComparison.Ordinal));
        Assert.Equal((added, changed, removed), (version.AddedCount, version.ChangedCount, version.RemovedCount));
    }

    [Theory]
    [InlineData(RatesKey, "so the rates of 1 April to 7 May 2026 are not seeded")]
    [InlineData(ExemptionsKey, "so that row ends on 7 May 2026: no row means none is seeded, never that no employee is exempt")]
    [InlineData(CoverageKey, "so the limits end on 7 May 2026: no limit means none is seeded, never that every employee is covered or that none is")]
    [InlineData(PeriodsKey, "so the rows end on 20 November 2026, the earlier day on which that year may end, and no period is seeded after it")]
    [InlineData(DueDatesKey, "so their rows end on 20 November 2026, the earlier day on which that year may end, and the Code's own row runs on")]
    public async Task Description_OfEachSetThatHoldsLessThanTheWholeWindow_SaysThatAMissingRowIsNotSeeded(string key, string words)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, Today, TestContext.Current.CancellationToken);

        Assert.Contains(words, set!.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Periods_FromTheDayAfterTheEarlierEndOfTheYearOfSection164_AreAVersionWithNoPeriod()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var periods = await StoreOf(scope).GetAsOfAsync<EsiContributionPeriods>(new DateOnly(2026, 11, 21), TestContext.Current.CancellationToken);

        Assert.NotNull(periods);
        Assert.Equal((new DateOnly(2026, 11, 21), (DateOnly?)null), (periods.Version.From, periods.Version.Until));
        Assert.Empty(periods.Periods);
    }

    [Fact]
    public async Task Sets_OnTheDayBeforeTheWindowOpens_HoldNothing()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = StoreOf(scope);
        var day = FirstDay.AddDays(-1);

        Assert.Null(await store.GetAsOfAsync<EsiContributionRates>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<EsiContributionExemptions>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<EsiAverageDailyWageDivisors>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<EsiCoverageWageLimits>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<EsiContributionPeriods>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<EsiContributionDueDates>(day, TestContext.Current.CancellationToken));
    }

    private static IStatutoryParameters StoreOf(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

    private static DateOnly Day(string value) => DateOnly.Parse(value, CultureInfo.InvariantCulture);

    private static decimal Number(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private static Money? Rupees(string? value) => value is null ? null : new Money(Number(value), Currency.Inr);

    private static DayAndMonth? DayMonth(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return DayAndMonth.TryParse(value, out var day) ? day : throw new ArgumentException($"'{value}' is not a day and month written MM-dd.", nameof(value));
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
