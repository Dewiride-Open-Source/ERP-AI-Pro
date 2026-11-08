using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Gratuity;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Labour;

// Golden values of gratuity: Chapter V of the Code on Social Security, 2020 and section 2(o)(c) of the Industrial Relations Code,
// 2020 for every establishment, and the Social Security (Central) Rules, 2026 where the Central Government is the appropriate
// Government, each pinned beside its instrument so that a wrong edit of a seed file fails a test (ADR-0063). The Codes' rules hold
// from 1 April 2026, where the ERP's holding starts, and the central rules' from their publication on 8 May 2026.
[Collection(StatutoryRows.Name)]
public sealed class GratuityGoldenTests(ErpApiFactory factory)
{
    private const string RulesKey = "gratuity.rules";

    private const string CodeOnSocialSecurity = "Code on Social Security, 2020 (No. 36 of 2020), 28 September 2020, Gazette of India, Extraordinary, Part II, Section 1, No. 61: sections 1(4) and (8), 53 to 56 and 164 and the First Schedule";

    private const string CodeCommencement = "S.O. 5319(E), 21 November 2025, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3(ii), No. 5143, as corrected by S.O. 5936(E), 19 December 2025, No. 5739: the Central Government “hereby appoints the 21st day of November, 2025 as the date on which the following provisions of the said Code, shall come into force”";

    private const string IndustrialRelationsCode = "Industrial Relations Code, 2020 (No. 35 of 2020), 28 September 2020, Gazette of India, Extraordinary, Part II, Section 1, No. 60: section 2(o)";

    private const string GratuityCeiling = "S.O. 1420(E), 29 March 2018, Ministry of Labour and Employment, under section 4(3) of the Payment of Gratuity Act, 1972, Gazette of India, Extraordinary, Part II, Section 3(ii), No. 1283";

    private const string SocialSecurityCentralRules = "Social Security (Central) Rules, 2026, G.S.R. 344(E), 8 May 2026, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3(i), No. 310: rule 33(1)(a) and (2)(a), superseding the Payment of Gratuity (Central) Rules, 1972";

    private static readonly DateOnly FirstDay = new(2026, 4, 1);

    private static readonly DateOnly CentralRulesDay = new(2026, 5, 8);

    [Theory]
    [InlineData("first-schedule-V(a)", null, null, null, null, null, "(a) every factory, mine, oilfield, plantation, port and railway company; and", "Code on Social Security, 2020, First Schedule, Chapter V, column (3), entry (a), applied by section 1(4)")]
    [InlineData("first-schedule-V(b)", 10, GratuityUnit.Employees, GratuityBound.AtLeast, null, null, "(b) every shop or establishment in which ten or more employees are employed, or were employed, on any day of the preceding twelve months;", "Code on Social Security, 2020, First Schedule, Chapter V, column (3), entry (b), applied by section 1(4)")]
    [InlineData("1(8)", null, null, null, null, null, "shall continue to be applied thereafter even if the number of employees therein at any subsequent time falls below the threshold specified in the First Schedule", "Code on Social Security, 2020, section 1(8)")]
    [InlineData("53(1)", 5, GratuityUnit.Years, GratuityBound.AtLeast, null, null, "after he has rendered continuous service for not less than five years,—", "Code on Social Security, 2020, section 53(1)")]
    [InlineData("53(1)-proviso-1", 3, GratuityUnit.Years, GratuityBound.AtLeast, null, null, "the expression “five years” occurring in this sub-section shall be deemed to be three years:", "Code on Social Security, 2020, section 53(1), first proviso")]
    [InlineData("53(1)-proviso-2", null, null, null, null, null, "the completion of continuous service of five years shall not be necessary where the termination of the employment of any employee is due to death or disablement or expiration of fixed term employment", "Code on Social Security, 2020, section 53(1), second proviso")]
    [InlineData("irc-2(o)(c)", 1, GratuityUnit.Years, GratuityBound.AtLeast, null, null, "(c) he shall be eligible for gratuity if he renders service under the contract for a period of one year;", "Industrial Relations Code, 2020, section 2(o), proviso, clause (c)")]
    [InlineData("53(2)", 15, GratuityUnit.DaysWages, null, null, null, "the employer shall pay gratuity to an employee at the rate of fifteen days' wages or such number of days as may be notified by the Central Government, based on the rate of wages last drawn", "Code on Social Security, 2020, section 53(2)")]
    [InlineData("53(2)-part-of-a-year", 6, GratuityUnit.Months, GratuityBound.MoreThan, null, null, "(2) For every completed year of service or part thereof in excess of six months,", "Code on Social Security, 2020, section 53(2), “part thereof in excess of six months”")]
    [InlineData("53(2)-proviso-1", 3, GratuityUnit.Months, null, null, null, "daily wages shall be computed on the average of the total wages received by him for a period of three months immediately preceding the termination of his employment", "Code on Social Security, 2020, section 53(2), first proviso")]
    [InlineData("53(2)-proviso-2", 7, GratuityUnit.DaysWages, null, null, null, "the employer shall pay the gratuity at the rate of seven days' wages for each season:", "Code on Social Security, 2020, section 53(2), second proviso")]
    [InlineData("53(2)-proviso-3", null, null, null, null, null, "in the case of an employee employed on fixed term employment or a deceased employee, the employer shall pay gratuity on pro rata basis.", "Code on Social Security, 2020, section 53(2), third proviso")]
    [InlineData("53(3)", null, null, GratuityBound.AtMost, null, "2000000", "the amount of gratuity payable to an employee under the said Act shall not exceed twenty lakh rupees.", "Code on Social Security, 2020, section 53(3), with S.O. 1420(E), 29 March 2018, under section 4(3) of the Payment of Gratuity Act, 1972, kept in force by section 164(2)(a)")]
    [InlineData("53(4)", null, null, null, null, null, "his wages for the period subsequent to his disablement shall be taken to be the wages as so reduced.", "Code on Social Security, 2020, section 53(4)")]
    [InlineData("53-explanation-1", null, null, null, null, null, "employee does not include any such person who holds a post under the Central Government or a State Government and is governed by any other Act or by any rules providing for payment of gratuity.", "Code on Social Security, 2020, section 53, Explanation 1")]
    [InlineData("53-explanation-3", 26, GratuityUnit.MonthlyRateDivisor, null, null, null, "by dividing the monthly rate of wages last drawn by him by twenty-six and multiplying the quotient by fifteen.", "Code on Social Security, 2020, section 53, Explanation 3")]
    [InlineData("54(B)(a)(i)", 190, GratuityUnit.Days, GratuityBound.AtLeast, null, null, "(i) one hundred and ninety days, in the case of any employee employed below the ground in a mine or in an establishment which works for less than six days in a week; and", "Code on Social Security, 2020, section 54(B)(a)(i)")]
    [InlineData("54(B)(a)(ii)", 240, GratuityUnit.Days, GratuityBound.AtLeast, null, null, "(ii) two hundred and forty days, in any other case;", "Code on Social Security, 2020, section 54(B)(a)(ii)")]
    [InlineData("54(B)(b)(i)", 95, GratuityUnit.Days, GratuityBound.AtLeast, null, null, "(i) ninety-five days, in the case of an employee employed below the ground in a mine or in an establishment which works for less than six days in a week; and", "Code on Social Security, 2020, section 54(B)(b)(i)")]
    [InlineData("54(B)(b)(ii)", 120, GratuityUnit.Days, GratuityBound.AtLeast, null, null, "(ii) one hundred and twenty days, in any other case.", "Code on Social Security, 2020, section 54(B)(b)(ii)")]
    [InlineData("54(B)-explanation", 26, GratuityUnit.Weeks, GratuityBound.AtMost, null, null, "(iv) in the case of a female, she has been on maternity leave; so, however, that the total period of such maternity leave does not exceed twenty-six weeks;", "Code on Social Security, 2020, section 54(B), Explanation, its clause (iv) for the weeks of maternity leave")]
    [InlineData("54(C)", null, null, GratuityBound.AtLeast, "75", null, "if he has actually worked for not less than seventy-five per cent. of the number of days on which the establishment was in operation during such period.", "Code on Social Security, 2020, section 54(C)")]
    [InlineData("55(1)", 1, GratuityUnit.Years, GratuityBound.AtLeast, null, null, "55. (1) Each employee, who has completed one year of service, shall make, a nomination", "Code on Social Security, 2020, section 55(1)")]
    [InlineData("56(3)", 30, GratuityUnit.Days, GratuityBound.AtMost, null, null, "(3) The employer shall arrange to pay the amount of gratuity within thirty days from the date it becomes payable", "Code on Social Security, 2020, section 56(3)")]
    [InlineData("56(4)", null, null, null, null, null, "simple interest at such rate, not exceeding the rate notified by the Central Government from time to time for repayment of long term deposits:", "Code on Social Security, 2020, section 56(4) and its proviso")]
    [InlineData("56(8)", 60, GratuityUnit.Days, GratuityBound.AtMost, null, null, "may, within sixty days from the date of the receipt of the order, prefer an appeal", "Code on Social Security, 2020, section 56(8)")]
    [InlineData("56(8)-proviso-1", 60, GratuityUnit.Days, GratuityBound.AtMost, null, null, "extend the said period by a further period of sixty days:", "Code on Social Security, 2020, section 56(8), first proviso")]
    public async Task Rule_OfTheCodesOnTheFirstDay_IsTheFigureAndWordsItsInstrumentSets(string provision, int? count, GratuityUnit? unit, GratuityBound? bound, string? percent, string? amount, string words, string source)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var rules = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<GratuityRules>(FirstDay, TestContext.Current.CancellationToken))!.Rules;

        var found = Assert.Single(rules, candidate => candidate.Provision == provision);
        Assert.Equal(GratuitySphere.EveryEstablishment, found.Sphere);
        Assert.Equal((count, unit, bound), (found.Count, found.Unit, found.Bound));
        Assert.Equal((Percent(percent), Rupees(amount)), (found.Percent, found.Amount));
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.Equal(source, await SourceAsync(scope, provision, FirstDay));
    }

    [Theory]
    [InlineData("rule-33(1)(a)-proviso-2", 6, GratuityUnit.Months, GratuityBound.MoreThan, "if he renders service under the contract for a period of at least one year and for subsequent period in excess of six months and more, but less than one year, shall be rounded off to one additional year.", "Social Security (Central) Rules, 2026, rule 33(1)(a), second proviso")]
    [InlineData("rule-33(2)(a)", 15, GratuityUnit.Days, GratuityBound.AtMost, "(2) (a) within fifteen days of the receipt of an application under sub-rule (1) for payment of gratuity, the employer shall—", "Social Security (Central) Rules, 2026, rule 33(2)(a)")]
    [InlineData("rule-33(2)(a)(i)", 30, GratuityUnit.Days, GratuityBound.AtMost, "fixing a date, not being later than the thirtieth day after the date of receipt of the application, for payment thereof; or", "Social Security (Central) Rules, 2026, rule 33(2)(a)(i)")]
    public async Task Rule_OfTheCentralRulesFromTheirPublication_IsTheFigureAndWordsTheyWriteForTheCentralSphere(string provision, int count, GratuityUnit unit, GratuityBound bound, string words, string source)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var rules = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<GratuityRules>(CentralRulesDay, TestContext.Current.CancellationToken))!.Rules;

        var found = Assert.Single(rules, candidate => candidate.Provision == provision);
        Assert.Equal((GratuitySphere.CentralSphere, (int?)count, (GratuityUnit?)unit, (GratuityBound?)bound), (found.Sphere, found.Count, found.Unit, found.Bound));
        Assert.Equal(((Percentage?)null, (Money?)null), (found.Percent, found.Amount));
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.Equal(source, await SourceAsync(scope, provision, CentralRulesDay));
    }

    [Theory]
    [InlineData("2026-04-01", 27, 0)]
    [InlineData("2026-05-07", 27, 0)]
    [InlineData("2026-05-08", 30, 3)]
    public async Task Rules_OnTheFirstDayOfEachVersionAndTheDayBefore_AreTheRulesInForceThatDay(string day, int rules, int centralRules)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<GratuityRules>(Day(day), TestContext.Current.CancellationToken);

        Assert.NotNull(set);
        Assert.Equal((rules, centralRules), (set.Rules.Count, set.Rules.Count(rule => rule.Sphere == GratuitySphere.CentralSphere)));
    }

    [Fact]
    public async Task Rules_OnTheDayBeforeTheFirstVersion_HoldNothing()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        Assert.Null(await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<GratuityRules>(FirstDay.AddDays(-1), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("2026-04-01", "2026-05-07", 27, CodeOnSocialSecurity + "|" + CodeCommencement + "|" + IndustrialRelationsCode + "|" + GratuityCeiling)]
    [InlineData("2026-05-08", null, 3, SocialSecurityCentralRules)]
    public async Task Version_OfTheRules_CitesItsInstrumentsInOrderAndAddsItsRules(string from, string? until, int added, string instruments)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(RulesKey, FirstDay, TestContext.Current.CancellationToken);

        Assert.NotNull(set);
        Assert.Equal((ParameterSetBasis.Law, ParameterAsOf.EmploymentEndDate), (set.Basis, set.AsOf));
        Assert.Equal([CentralRulesDay, FirstDay], set.Versions.Select(version => version.From));
        var version = Assert.Single(set.Versions, candidate => candidate.From == Day(from));
        Assert.Equal((until is null ? (DateOnly?)null : Day(until), added, 0, 0), (version.Until, version.AddedCount, version.ChangedCount, version.RemovedCount));
        var cited = instruments.Split('|');
        Assert.Equal(cited.Length, version.Instruments.Count);
        Assert.All(cited.Zip(version.Instruments), pair => Assert.StartsWith(pair.First, pair.Second, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Description_OfTheRules_SaysNoRateOfInterestOnGratuityPaidLateIsHeld()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(RulesKey, FirstDay, TestContext.Current.CancellationToken);

        Assert.Contains("No rate of interest on gratuity paid late is held", set!.Description, StringComparison.Ordinal);
        Assert.EndsWith("The set answers for the day employment ends.", set.Description, StringComparison.Ordinal);
    }

    private static DateOnly Day(string day) => DateOnly.Parse(day, CultureInfo.InvariantCulture);

    private static Percentage? Percent(string? value) => value is null ? null : new Percentage(decimal.Parse(value, CultureInfo.InvariantCulture));

    private static Money? Rupees(string? value) => value is null ? null : new Money(decimal.Parse(value, CultureInfo.InvariantCulture), Currency.Inr);

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string provision, DateOnly on)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(RulesKey, on, TestContext.Current.CancellationToken);

        return Assert.Single(view!.Rows, row => row.Key == provision).Source;
    }
}
