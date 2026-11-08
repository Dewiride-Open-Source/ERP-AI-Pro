using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProvidentFund.Contributions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProvidentFund.Remittance;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Labour;

// Golden values of the provident fund under the Code on Social Security, 2020 from 1 April 2026: the contribution rates with their
// wage limits, the establishments that contribute ten per cent, the wage ceiling and the membership limits, the administrative and
// inspection charges, the remittance days and the rounding, each pinned beside its instrument so that a wrong edit of a seed file
// fails a test (ADR-0063). Every entry is pinned on the first day of its version and on the day before the next, the sets hold
// nothing before 1 April 2026, and every version's instruments are pinned in order.
[Collection(StatutoryRows.Name)]
public sealed class ProvidentFundGoldenTests(ErpApiFactory factory)
{
    private const string RatesKey = "provident-fund.contribution-rates";

    private const string EstablishmentsKey = "provident-fund.ten-per-cent-establishments";

    private const string LimitsKey = "provident-fund.membership-limits";

    private const string ChargesKey = "provident-fund.charges";

    private const string DueDatesKey = "provident-fund.due-dates";

    private const string RoundingKey = "provident-fund.rounding";

    private const string CodeOnRatesAndContinuation = "Code on Social Security, 2020 (36 of 2020), Gazette of India, Extraordinary, Part II, Section 1, No. 61, 29 September 2020: section 16(1), and section 164(2)";

    private const string CodeOnContinuation = "Code on Social Security, 2020 (36 of 2020), Gazette of India, Extraordinary, Part II, Section 1, No. 61, 29 September 2020: section 164(2)";

    private const string CodeOnTheTenPerCent = "Code on Social Security, 2020 (36 of 2020), Gazette of India, Extraordinary, Part II, Section 1, No. 61, 29 September 2020: section 16(1)(a) and its first proviso";

    private const string CodesCommencement = "S.O. 5319(E), 21 November 2025, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (ii), No. 5143, as corrected by S.O. 5936(E), 19 December 2025, No. 5739 of 20 December 2025";

    private const string TenPerCentNotification = "S.O. 3582(E), 1 July 2026, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (ii), No. 3447";

    private const string ContinuedRateSchemes = "Employees' Provident Funds Scheme, 1952, paragraphs 26A and 29, and Employees' Pension Scheme, 1995, paragraph 3, with S.O. 2061(E) of 3 May 2023";

    private const string ProvidentFundsScheme2026 = "Employees' Provident Funds Scheme, 2026, G.S.R. 525(E), 29 June 2026, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (i), No. 473";

    private const string PensionScheme2026 = "Employees' Pension Scheme, 2026, G.S.R. 527(E), 29 June 2026, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (i), No. 475";

    private const string InsuranceScheme2026 = "Employees' Deposit-Linked Insurance Scheme, 2026, G.S.R. 526(E), 29 June 2026, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (i), No. 474";

    private const string PensionRateNotification = "S.O. 3580(E), 1 July 2026, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (ii), No. 3445";

    private const string InsuranceRateNotification = "S.O. 3581(E), 1 July 2026, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (ii), No. 3446";

    private const string FirstWageCeiling = "S.O. 2702(E), 29 May 2026, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (ii), No. 2609";

    private const string SecondWageCeiling = "S.O. 5109(E), 17 September 2026, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (ii), No. 4918";

    private const string CentralGovernmentLimit = "G.S.R. 890(E), 15 October 2026, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (i), No. 819";

    private const string NewCeilingMembership = "G.S.R. 847(E), 25 September 2026, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (i), No. 777";

    private const string ContinuedMembershipSchemes = "Employees' Provident Funds Scheme, 1952, paragraph 2(f)(ii) and its provision for persons with disability, and Employees' Pension Scheme, 1995, paragraph 6";

    private const string AdministrativeCharges = "S.O. 2011(E), 21 May 2018, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (ii), No. 1818";

    private const string InspectionCharges = "S.O. 2701(E), 29 May 2026, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (ii), No. 2608";

    private const string InspectionCorrigendum = "S.O. 4602(E), 20 August 2026, corrigendum, Ministry of Labour and Employment, Gazette of India, Extraordinary, Part II, Section 3, Sub-section (ii), No. 4422";

    private const string NotificationsContinued = "Code on Social Security, 2020, section 164(2)(a)";

    private const string ContinuedDueDateSchemes = "Employees' Provident Funds Scheme, 1952, paragraph 38(1), and Employees' Deposit-Linked Insurance Scheme, 1976, paragraph 8(1)";

    private const string ContinuedRoundingSchemes = "Employees' Provident Funds Scheme, 1952, paragraph 29, Employees' Pension Scheme, 1995, paragraph 3(3), and Employees' Deposit-Linked Insurance Scheme, 1976, paragraph 7(2)";

    private const string MemberPayAboveFifteenThousand = "where the monthly pay of such a member exceeds fifteen thousand rupees the contribution payable by him";

    private const string PensionPayUpToFifteenThousand = "limited to the amount payable on his pay of fifteen thousand rupees only.";

    private const string FirstCeilingWords = "rupees fifteen thousand (₹15,000) per month as the wage ceiling for the purposes of Chapter III";

    private const string SecondCeilingWords = "rupees twenty-five thousand (₹25,000) per month as the wage ceiling for the purposes of Chapter III";

    private const string JointOptionWageWords = "such increased contribution shall be applicable to wages exceeding fifteen thousand rupees per month.";

    private const string TwelvePerCentWords = "shall be at the rate of twelve per cent of the wages payable to the employee";

    private const string TenPerCentWords = "the rate of contribution shall be ten per cent in respect of the class of establishments notified by the Central Government";

    private const string EqualContributionWords = "the employees' contribution shall be equal to the employer's contribution in respect of such employee";

    private const string PensionShareWords = "a part of contribution of eight and thirty-three hundredths per cent. of the wages of employee up to wage ceiling";

    private const string JointOptionWords = "the contribution of the employer shall be nine and forty-nine hundredths per cent. of the wages of each member";

    private const string CentralGovernmentWords = "shall contribute to the Scheme at the rate of one and sixteen hundredths per cent. of the wages of the members";

    private const string InsuranceRateWords = "hereby specifies one-half per cent of the wages";

    private const string PensionShareSource = "The rate held is that of paragraph 4(1), which directs the remittance; S.O. 3580(E), under section 16(1)(b) of the Code, writes it “eight and one-third per cent.”";

    private const string SecondCeilingSource = "the wage ceiling of S.O. 5109(E), 17 September 2026";

    private const string OtherEstablishmentsWords = "five hundred rupees per month per establishment for other establishments.";

    private const string NonFunctionalWords = "seventy-five rupees per month for every non-functional establishment having no contributory member";

    private const string FixingUnder2026SchemeWords = "fix the percentage of administrative charges payable under sub-paragraph (2) of paragraph 28.";

    private const string AdministrativeChargesSource = "S.O. 2011(E), 21 May 2018, in force 1 June 2018, under the Explanation to paragraph 30 read with paragraph 39 of the Employees' Provident Funds Scheme, 1952";

    private const string AdministrativeChargesContinuedSource = "S.O. 2011(E), 21 May 2018, continued by section 164(2)(a) of the Code on Social Security, 2020, read with paragraphs 28(2) and 29(1) of the Employees' Provident Funds Scheme, 2026";

    private const string ProvidentFundInspectionWords = "Rate of Inspection charges (per cent. of wages): 0.18. Minimum Inspection Charge (in Rupees): 8750.";

    private const string InsuranceInspectionWords = "Rate of Inspection charges (per cent. of wages): 0.005. Minimum Inspection Charge (in Rupees): 1250.";

    private static readonly DateOnly FirstDay = new(2026, 4, 1);

    private static readonly DateOnly Today = new(2026, 11, 8);

    [Theory]
    [InlineData("2026-04-01", "provident-fund-employer", ContributionPayer.Employer, ContributionFund.ProvidentFund, ContributionShare.PerCentOfWages, "12", "15000", null, "specifies twelve percent as the contribution to be paid by the employer and the employees", MemberPayAboveFifteenThousand, "S.O. 3582(E), paragraph 1, deemed in force 21 November 2025; the wage limit of the Employees' Provident Funds Scheme, 1952, paragraph 26A, proviso")]
    [InlineData("2026-06-28", "provident-fund-employer", ContributionPayer.Employer, ContributionFund.ProvidentFund, ContributionShare.PerCentOfWages, "12", "15000", null, "specifies twelve percent as the contribution to be paid by the employer and the employees", MemberPayAboveFifteenThousand, "S.O. 3582(E), paragraph 1, deemed in force 21 November 2025; the wage limit of the Employees' Provident Funds Scheme, 1952, paragraph 26A, proviso")]
    [InlineData("2026-04-01", "provident-fund-employer-ten-per-cent", ContributionPayer.Employer, ContributionFund.ProvidentFund, ContributionShare.PerCentOfWages, "10", "15000", null, "The contributions payable by the employer under the Scheme shall be at the rate of ten per cent", MemberPayAboveFifteenThousand, "S.O. 3582(E), paragraph 2, deemed in force 21 November 2025; Employees' Provident Funds Scheme, 1952, paragraph 29")]
    [InlineData("2026-06-28", "provident-fund-employer-ten-per-cent", ContributionPayer.Employer, ContributionFund.ProvidentFund, ContributionShare.PerCentOfWages, "10", "15000", null, "The contributions payable by the employer under the Scheme shall be at the rate of ten per cent", MemberPayAboveFifteenThousand, "S.O. 3582(E), paragraph 2, deemed in force 21 November 2025; Employees' Provident Funds Scheme, 1952, paragraph 29")]
    [InlineData("2026-04-01", "provident-fund-employee", ContributionPayer.Employee, ContributionFund.ProvidentFund, ContributionShare.EqualToEmployers, null, "15000", null, "the employee's contribution shall be equal to the contribution payable by the employer in respect of him", MemberPayAboveFifteenThousand, "Code on Social Security, 2020, section 16(1)(a), with its first proviso and S.O. 3582(E)")]
    [InlineData("2026-06-28", "provident-fund-employee", ContributionPayer.Employee, ContributionFund.ProvidentFund, ContributionShare.EqualToEmployers, null, "15000", null, "the employee's contribution shall be equal to the contribution payable by the employer in respect of him", MemberPayAboveFifteenThousand, "Code on Social Security, 2020, section 16(1)(a), with its first proviso and S.O. 3582(E)")]
    [InlineData("2026-04-01", "pension-fund-employer", ContributionPayer.Employer, ContributionFund.PensionFund, ContributionShare.PartOfEmployers, "8.33", "15000", null, "a part of contribution representing 8.33 per cent. of the Employee's pay shall be remitted by the employer", PensionPayUpToFifteenThousand, "Employees' Pension Scheme, 1995, paragraph 3(1) and the proviso to paragraph 3(2)")]
    [InlineData("2026-06-28", "pension-fund-employer", ContributionPayer.Employer, ContributionFund.PensionFund, ContributionShare.PartOfEmployers, "8.33", "15000", null, "a part of contribution representing 8.33 per cent. of the Employee's pay shall be remitted by the employer", PensionPayUpToFifteenThousand, "Employees' Pension Scheme, 1995, paragraph 3(1) and the proviso to paragraph 3(2)")]
    [InlineData("2026-04-01", "pension-fund-employer-joint-option", ContributionPayer.Employer, ContributionFund.PensionFund, ContributionShare.PartOfEmployers, "9.49", null, "15000", "the employer's contribution shall be nine and forty-ninth per cent. (9.49%) of the basic wages", JointOptionWageWords, "Employees' Pension Scheme, 1995, as EPFO's consolidated text prints S.O. 2061(E) of 3 May 2023")]
    [InlineData("2026-06-28", "pension-fund-employer-joint-option", ContributionPayer.Employer, ContributionFund.PensionFund, ContributionShare.PartOfEmployers, "9.49", null, "15000", "the employer's contribution shall be nine and forty-ninth per cent. (9.49%) of the basic wages", JointOptionWageWords, "Employees' Pension Scheme, 1995, as EPFO's consolidated text prints S.O. 2061(E) of 3 May 2023")]
    [InlineData("2026-04-01", "pension-fund-central-government", ContributionPayer.CentralGovernment, ContributionFund.PensionFund, ContributionShare.PerCentOfWages, "1.16", "15000", null, "The Central Government shall also contribute at the rate of 1.16 per cent. of the pay of the members", PensionPayUpToFifteenThousand, "Employees' Pension Scheme, 1995, paragraph 3(2) and its proviso")]
    [InlineData("2026-06-28", "pension-fund-central-government", ContributionPayer.CentralGovernment, ContributionFund.PensionFund, ContributionShare.PerCentOfWages, "1.16", "15000", null, "The Central Government shall also contribute at the rate of 1.16 per cent. of the pay of the members", PensionPayUpToFifteenThousand, "Employees' Pension Scheme, 1995, paragraph 3(2) and its proviso")]
    [InlineData("2026-06-29", "provident-fund-employer", ContributionPayer.Employer, ContributionFund.ProvidentFund, ContributionShare.PerCentOfWages, "12", "15000", null, TwelvePerCentWords, FirstCeilingWords, "Employees' Provident Funds Scheme, 2026, paragraph 18(2) and (3), the latter as corrected by G.S.R. 703(E), item 8")]
    [InlineData("2026-09-16", "provident-fund-employer", ContributionPayer.Employer, ContributionFund.ProvidentFund, ContributionShare.PerCentOfWages, "12", "15000", null, TwelvePerCentWords, FirstCeilingWords, "Employees' Provident Funds Scheme, 2026, paragraph 18(2) and (3), the latter as corrected by G.S.R. 703(E), item 8")]
    [InlineData("2026-06-29", "provident-fund-employer-ten-per-cent", ContributionPayer.Employer, ContributionFund.ProvidentFund, ContributionShare.PerCentOfWages, "10", "15000", null, TenPerCentWords, FirstCeilingWords, "Employees' Provident Funds Scheme, 2026, paragraph 18(2), first proviso, and 18(3), as corrected by G.S.R. 703(E), item 8; S.O. 3582(E), paragraph 2")]
    [InlineData("2026-09-16", "provident-fund-employer-ten-per-cent", ContributionPayer.Employer, ContributionFund.ProvidentFund, ContributionShare.PerCentOfWages, "10", "15000", null, TenPerCentWords, FirstCeilingWords, "Employees' Provident Funds Scheme, 2026, paragraph 18(2), first proviso, and 18(3), as corrected by G.S.R. 703(E), item 8; S.O. 3582(E), paragraph 2")]
    [InlineData("2026-06-29", "provident-fund-employee", ContributionPayer.Employee, ContributionFund.ProvidentFund, ContributionShare.EqualToEmployers, null, "15000", null, EqualContributionWords, FirstCeilingWords, "Employees' Provident Funds Scheme, 2026, paragraph 18(2) and (3)")]
    [InlineData("2026-09-16", "provident-fund-employee", ContributionPayer.Employee, ContributionFund.ProvidentFund, ContributionShare.EqualToEmployers, null, "15000", null, EqualContributionWords, FirstCeilingWords, "Employees' Provident Funds Scheme, 2026, paragraph 18(2) and (3)")]
    [InlineData("2026-06-29", "pension-fund-employer", ContributionPayer.Employer, ContributionFund.PensionFund, ContributionShare.PartOfEmployers, "8.33", "15000", null, PensionShareWords, FirstCeilingWords, PensionShareSource)]
    [InlineData("2026-09-16", "pension-fund-employer", ContributionPayer.Employer, ContributionFund.PensionFund, ContributionShare.PartOfEmployers, "8.33", "15000", null, PensionShareWords, FirstCeilingWords, PensionShareSource)]
    [InlineData("2026-06-29", "pension-fund-employer-joint-option", ContributionPayer.Employer, ContributionFund.PensionFund, ContributionShare.PartOfEmployers, "9.49", null, "15000", JointOptionWords, JointOptionWageWords, "Employees' Pension Scheme, 2026, paragraph 4(2), second proviso")]
    [InlineData("2026-09-16", "pension-fund-employer-joint-option", ContributionPayer.Employer, ContributionFund.PensionFund, ContributionShare.PartOfEmployers, "9.49", null, "15000", JointOptionWords, JointOptionWageWords, "Employees' Pension Scheme, 2026, paragraph 4(2), second proviso")]
    [InlineData("2026-06-29", "pension-fund-central-government", ContributionPayer.CentralGovernment, ContributionFund.PensionFund, ContributionShare.PerCentOfWages, "1.16", "15000", null, CentralGovernmentWords, "the contribution payable by the employer and the Central Government shall be limited to the amount payable on wages up to such wage ceiling", "Employees' Pension Scheme, 2026, paragraph 4(2) and its first proviso, “pay” read “wages” by G.S.R. 704(E), item 2")]
    [InlineData("2026-09-16", "pension-fund-central-government", ContributionPayer.CentralGovernment, ContributionFund.PensionFund, ContributionShare.PerCentOfWages, "1.16", "15000", null, CentralGovernmentWords, "the contribution payable by the employer and the Central Government shall be limited to the amount payable on wages up to such wage ceiling", "Employees' Pension Scheme, 2026, paragraph 4(2) and its first proviso, “pay” read “wages” by G.S.R. 704(E), item 2")]
    [InlineData("2026-06-29", "insurance-fund-employer", ContributionPayer.Employer, ContributionFund.InsuranceFund, ContributionShare.PerCentOfWages, "0.5", "15000", null, InsuranceRateWords, FirstCeilingWords, "S.O. 3581(E), under section 16(1)(c) of the Code on Social Security, 2020, in force 29 June 2026")]
    [InlineData("2026-09-16", "insurance-fund-employer", ContributionPayer.Employer, ContributionFund.InsuranceFund, ContributionShare.PerCentOfWages, "0.5", "15000", null, InsuranceRateWords, FirstCeilingWords, "S.O. 3581(E), under section 16(1)(c) of the Code on Social Security, 2020, in force 29 June 2026")]
    [InlineData("2026-09-17", "provident-fund-employer", ContributionPayer.Employer, ContributionFund.ProvidentFund, ContributionShare.PerCentOfWages, "12", "25000", null, TwelvePerCentWords, SecondCeilingWords, SecondCeilingSource)]
    [InlineData("2026-09-17", "provident-fund-employer-ten-per-cent", ContributionPayer.Employer, ContributionFund.ProvidentFund, ContributionShare.PerCentOfWages, "10", "25000", null, TenPerCentWords, SecondCeilingWords, SecondCeilingSource)]
    [InlineData("2026-09-17", "provident-fund-employee", ContributionPayer.Employee, ContributionFund.ProvidentFund, ContributionShare.EqualToEmployers, null, "25000", null, EqualContributionWords, SecondCeilingWords, SecondCeilingSource)]
    [InlineData("2026-09-17", "pension-fund-employer", ContributionPayer.Employer, ContributionFund.PensionFund, ContributionShare.PartOfEmployers, "8.33", "25000", null, PensionShareWords, "the contribution payable by the employer shall be limited to the amount payable on wages up to such wage ceiling", PensionShareSource)]
    [InlineData("2026-09-17", "pension-fund-employer-joint-option", ContributionPayer.Employer, ContributionFund.PensionFund, ContributionShare.PartOfEmployers, "9.49", null, "15000", JointOptionWords, JointOptionWageWords, "Employees' Pension Scheme, 2026, paragraph 4(2), second proviso")]
    [InlineData("2026-09-17", "pension-fund-central-government", ContributionPayer.CentralGovernment, ContributionFund.PensionFund, ContributionShare.PerCentOfWages, "1.16", "15000", null, CentralGovernmentWords, "limited to wages upto rupees fifteen thousand (Rs. 15,000/-) per month", "as amended by G.S.R. 890(E), clauses (i) and (ii), in force 17 September 2026")]
    [InlineData("2026-09-17", "insurance-fund-employer", ContributionPayer.Employer, ContributionFund.InsuranceFund, ContributionShare.PerCentOfWages, "0.5", "25000", null, InsuranceRateWords, SecondCeilingWords, SecondCeilingSource)]
    public async Task Rate_OfEachContributionOnTheFirstDayOfItsVersionAndTheDayBeforeTheNext_IsTheRateAndWageLimitItsInstrumentsSet(string day, string contribution, ContributionPayer payer, ContributionFund fund, ContributionShare share, string? rate, string? wagesUpTo, string? wagesAbove, string words, string limitWords, string instrument)
    {
        var date = Day(day);
        await using var scope = factory.Services.CreateAsyncScope();

        var rates = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<ProvidentFundContributionRates>(date, TestContext.Current.CancellationToken))!.Rates;

        var found = Assert.Single(rates, candidate => candidate.Contribution == contribution);
        Assert.Equal((payer, fund, share), (found.Payer, found.Fund, found.Share));
        Assert.Equal(Percent(rate), found.Rate);
        Assert.Equal(Rupees(wagesUpTo), found.WagesUpTo);
        Assert.Equal(Rupees(wagesAbove), found.WagesAbove);
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.Contains(limitWords, found.Wording, StringComparison.Ordinal);
        Assert.Contains(instrument, await SourceAsync(scope, RatesKey, contribution, date), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-06-29", "insurance-fund-employer,pension-fund-central-government,pension-fund-employer,provident-fund-employee,provident-fund-employer,provident-fund-employer-ten-per-cent")]
    [InlineData("2026-09-16", "insurance-fund-employer,pension-fund-central-government,pension-fund-employer,provident-fund-employee,provident-fund-employer,provident-fund-employer-ten-per-cent")]
    [InlineData("2026-09-17", "insurance-fund-employer,pension-fund-employer,provident-fund-employee,provident-fund-employer,provident-fund-employer-ten-per-cent")]
    public async Task WageLimit_OfTheEntriesTheSchemesOf2026TieToTheWageCeiling_IsTheWageCeilingOfThatDay(string day, string contributions)
    {
        var date = Day(day);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        var rates = (await store.GetAsOfAsync<ProvidentFundContributionRates>(date, TestContext.Current.CancellationToken))!.Rates;
        var limits = (await store.GetAsOfAsync<ProvidentFundMembershipLimits>(date, TestContext.Current.CancellationToken))!.Limits;

        var ceiling = Assert.Single(limits, limit => limit.Test == MembershipTest.WageCeiling).Amount;
        Assert.Equal(contributions.Split(','), rates.Where(rate => rate.WagesUpTo == ceiling).Select(rate => rate.Contribution).Order(StringComparer.Ordinal));
        Assert.All(limits, limit => Assert.Equal(ceiling, limit.Amount));
    }

    [Theory]
    [InlineData("2(i)", "any establishment for which a resolution plan or repayment plan has been approved by the Adjudicating Authority established under the Insolvency and Bankruptcy Code, 2016 (31 of 2016)", "S.O. 3582(E), paragraph 2, item i")]
    [InlineData("2(ii)(a)", "any establishment in the jute Industry", "S.O. 3582(E), paragraph 2, item ii(a)")]
    [InlineData("2(ii)(b)", "any establishment in the beedi Industry", "S.O. 3582(E), paragraph 2, item ii(b)")]
    [InlineData("2(ii)(c)", "any establishment in the brick Industry", "S.O. 3582(E), paragraph 2, item ii(c)")]
    [InlineData("2(ii)(d)", "any establishment in the coir Industry other than the spinning sector", "S.O. 3582(E), paragraph 2, item ii(d)")]
    [InlineData("2(ii)(e)", "any establishment in the guar gum factories", "S.O. 3582(E), paragraph 2, item ii(e)")]
    public async Task Establishment_OfEachEntryOfTheNotification_IsTheClassItNames(string entry, string establishments, string source)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var entries = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<TenPerCentEstablishments>(FirstDay, TestContext.Current.CancellationToken))!.Entries;

        Assert.Equal(establishments, Assert.Single(entries, candidate => candidate.Entry == entry).Establishments);
        Assert.Equal(source, await SourceAsync(scope, EstablishmentsKey, entry, FirstDay));
    }

    [Theory]
    [InlineData("2026-04-01", "excluded-employee", "15000", MembershipTest.ExcludedAbove, "exceeds fifteen thousand rupees per month;", "Employees' Provident Funds Scheme, 1952, paragraph 2(f)(ii)")]
    [InlineData("2026-05-28", "excluded-employee", "15000", MembershipTest.ExcludedAbove, "exceeds fifteen thousand rupees per month;", "Employees' Provident Funds Scheme, 1952, paragraph 2(f)(ii)")]
    [InlineData("2026-05-29", "excluded-employee", "15000", MembershipTest.ExcludedAbove, "exceeds fifteen thousand rupees per month;", "Employees' Provident Funds Scheme, 1952, paragraph 2(f)(ii)")]
    [InlineData("2026-06-28", "excluded-employee", "15000", MembershipTest.ExcludedAbove, "exceeds fifteen thousand rupees per month;", "Employees' Provident Funds Scheme, 1952, paragraph 2(f)(ii)")]
    [InlineData("2026-04-01", "excluded-employee-with-disability", "25000", MembershipTest.ExcludedAbove, "a person with disability, whose pay at the time he is otherwise entitled to become a member of the Fund, exceeds twenty-five thousand rupees per month.", "the provision for persons with disability that EPFO's consolidated text prints under paragraph 83")]
    [InlineData("2026-05-28", "excluded-employee-with-disability", "25000", MembershipTest.ExcludedAbove, "a person with disability, whose pay at the time he is otherwise entitled to become a member of the Fund, exceeds twenty-five thousand rupees per month.", "the provision for persons with disability that EPFO's consolidated text prints under paragraph 83")]
    [InlineData("2026-05-29", "excluded-employee-with-disability", "25000", MembershipTest.ExcludedAbove, "a person with disability, whose pay at the time he is otherwise entitled to become a member of the Fund, exceeds twenty-five thousand rupees per month.", "the provision for persons with disability that EPFO's consolidated text prints under paragraph 83")]
    [InlineData("2026-06-28", "excluded-employee-with-disability", "25000", MembershipTest.ExcludedAbove, "a person with disability, whose pay at the time he is otherwise entitled to become a member of the Fund, exceeds twenty-five thousand rupees per month.", "the provision for persons with disability that EPFO's consolidated text prints under paragraph 83")]
    [InlineData("2026-04-01", "pension-scheme-member", "15000", MembershipTest.MemberUpTo, "and whose pay on such date is less than or equal to fifteen thousand rupees", "Employees' Pension Scheme, 1995, paragraph 6, printed as 7 in EPFO's consolidated text")]
    [InlineData("2026-05-28", "pension-scheme-member", "15000", MembershipTest.MemberUpTo, "and whose pay on such date is less than or equal to fifteen thousand rupees", "Employees' Pension Scheme, 1995, paragraph 6, printed as 7 in EPFO's consolidated text")]
    [InlineData("2026-05-29", "pension-scheme-member", "15000", MembershipTest.MemberUpTo, "and whose pay on such date is less than or equal to fifteen thousand rupees", "Employees' Pension Scheme, 1995, paragraph 6, printed as 7 in EPFO's consolidated text")]
    [InlineData("2026-06-28", "pension-scheme-member", "15000", MembershipTest.MemberUpTo, "and whose pay on such date is less than or equal to fifteen thousand rupees", "Employees' Pension Scheme, 1995, paragraph 6, printed as 7 in EPFO's consolidated text")]
    [InlineData("2026-05-29", "wage-ceiling", "15000", MembershipTest.WageCeiling, FirstCeilingWords, "S.O. 2702(E), 29 May 2026, under section 2(89) of the Code on Social Security, 2020")]
    [InlineData("2026-06-28", "wage-ceiling", "15000", MembershipTest.WageCeiling, FirstCeilingWords, "S.O. 2702(E), 29 May 2026, under section 2(89) of the Code on Social Security, 2020")]
    [InlineData("2026-06-29", "wage-ceiling", "15000", MembershipTest.WageCeiling, FirstCeilingWords, "S.O. 2702(E), 29 May 2026, under section 2(89) of the Code on Social Security, 2020")]
    [InlineData("2026-09-16", "wage-ceiling", "15000", MembershipTest.WageCeiling, FirstCeilingWords, "S.O. 2702(E), 29 May 2026, under section 2(89) of the Code on Social Security, 2020")]
    [InlineData("2026-06-29", "excluded-employee", "15000", MembershipTest.ExcludedAbove, "an employee other than the International Worker whose wage at the time he is otherwise entitled to become a member of the Fund, exceeds wage ceiling as prescribed under the Code;", "Employees' Provident Funds Scheme, 2026, paragraph 2(f)(i), as corrected by G.S.R. 703(E), item 1(ii), and paragraph 9(3); the wage ceiling of S.O. 2702(E)")]
    [InlineData("2026-09-16", "excluded-employee", "15000", MembershipTest.ExcludedAbove, "an employee other than the International Worker whose wage at the time he is otherwise entitled to become a member of the Fund, exceeds wage ceiling as prescribed under the Code;", "Employees' Provident Funds Scheme, 2026, paragraph 2(f)(i), as corrected by G.S.R. 703(E), item 1(ii), and paragraph 9(3); the wage ceiling of S.O. 2702(E)")]
    [InlineData("2026-06-29", "pension-scheme-member", "15000", MembershipTest.MemberUpTo, "and whose wages on such date is less than or equal to wage ceiling notified by the Central Government;", "Employees' Pension Scheme, 2026, paragraph 7(1)(i); the wage ceiling of S.O. 2702(E)")]
    [InlineData("2026-09-16", "pension-scheme-member", "15000", MembershipTest.MemberUpTo, "and whose wages on such date is less than or equal to wage ceiling notified by the Central Government;", "Employees' Pension Scheme, 2026, paragraph 7(1)(i); the wage ceiling of S.O. 2702(E)")]
    [InlineData("2026-09-17", "wage-ceiling", "25000", MembershipTest.WageCeiling, SecondCeilingWords, "S.O. 5109(E), 17 September 2026, under section 2(89) of the Code on Social Security, 2020, superseding S.O. 2702(E)")]
    [InlineData("2026-09-17", "excluded-employee", "25000", MembershipTest.ExcludedAbove, "an employee other than the International Worker whose wage at the time he is otherwise entitled to become a member of the Fund, exceeds wage ceiling as prescribed under the Code;", "paragraph 9(3); the wage ceiling of S.O. 5109(E), 17 September 2026")]
    [InlineData("2026-09-17", "pension-scheme-member", "25000", MembershipTest.MemberUpTo, "and whose wages on such date is less than or equal to wage ceiling notified by the Central Government;", "Employees' Pension Scheme, 2026, paragraph 7(1)(i); the wage ceiling of S.O. 5109(E)")]
    [InlineData("2026-09-17", "pension-scheme-member-on-new-ceiling", "25000", MembershipTest.MemberUpTo, "and whose wages on the date of notification of the new wage ceiling is less than or equal to wage ceiling notified by the Central Government.", "Employees' Pension Scheme, 2026, paragraph 7(1)(iii), inserted by G.S.R. 847(E), 25 September 2026, in force 17 September 2026")]
    public async Task Limit_OnTheFirstDayOfItsVersionAndTheDayBeforeTheNext_IsTheAmountItsInstrumentSets(string day, string limit, string amount, MembershipTest test, string words, string instrument)
    {
        var date = Day(day);
        await using var scope = factory.Services.CreateAsyncScope();

        var limits = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<ProvidentFundMembershipLimits>(date, TestContext.Current.CancellationToken))!.Limits;

        var found = Assert.Single(limits, candidate => candidate.Limit == limit);
        Assert.Equal((Amount(amount), test), (found.Amount, found.Test));
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.Contains(instrument, await SourceAsync(scope, LimitsKey, limit, date), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-04-01", "provident-fund-administrative", ChargeScheme.ProvidentFunds, ChargedEstablishments.OtherEstablishments, "0.5", "500", OtherEstablishmentsWords, AdministrativeChargesSource)]
    [InlineData("2026-05-28", "provident-fund-administrative", ChargeScheme.ProvidentFunds, ChargedEstablishments.OtherEstablishments, "0.5", "500", OtherEstablishmentsWords, AdministrativeChargesSource)]
    [InlineData("2026-05-29", "provident-fund-administrative", ChargeScheme.ProvidentFunds, ChargedEstablishments.OtherEstablishments, "0.5", "500", OtherEstablishmentsWords, AdministrativeChargesSource)]
    [InlineData("2026-06-28", "provident-fund-administrative", ChargeScheme.ProvidentFunds, ChargedEstablishments.OtherEstablishments, "0.5", "500", OtherEstablishmentsWords, AdministrativeChargesSource)]
    [InlineData("2026-04-01", "provident-fund-administrative-non-functional", ChargeScheme.ProvidentFunds, ChargedEstablishments.NonFunctionalWithoutMembers, "0.5", "75", NonFunctionalWords, AdministrativeChargesSource)]
    [InlineData("2026-05-28", "provident-fund-administrative-non-functional", ChargeScheme.ProvidentFunds, ChargedEstablishments.NonFunctionalWithoutMembers, "0.5", "75", NonFunctionalWords, AdministrativeChargesSource)]
    [InlineData("2026-05-29", "provident-fund-administrative-non-functional", ChargeScheme.ProvidentFunds, ChargedEstablishments.NonFunctionalWithoutMembers, "0.5", "75", NonFunctionalWords, AdministrativeChargesSource)]
    [InlineData("2026-06-28", "provident-fund-administrative-non-functional", ChargeScheme.ProvidentFunds, ChargedEstablishments.NonFunctionalWithoutMembers, "0.5", "75", NonFunctionalWords, AdministrativeChargesSource)]
    [InlineData("2026-05-29", "provident-fund-inspection", ChargeScheme.ProvidentFunds, ChargedEstablishments.Exempted, "0.18", "8750", ProvidentFundInspectionWords, "Table, Sl. No. (i), its rate “0.35” read “0.18” by the corrigendum S.O. 4602(E), 20 August 2026")]
    [InlineData("2026-06-28", "provident-fund-inspection", ChargeScheme.ProvidentFunds, ChargedEstablishments.Exempted, "0.18", "8750", ProvidentFundInspectionWords, "Table, Sl. No. (i), its rate “0.35” read “0.18” by the corrigendum S.O. 4602(E), 20 August 2026")]
    [InlineData("2026-05-29", "insurance-fund-inspection", ChargeScheme.DepositLinkedInsurance, ChargedEstablishments.Exempted, "0.005", "1250", InsuranceInspectionWords, "S.O. 2701(E), 29 May 2026, under section 143(6) of the Code on Social Security, 2020, Table, Sl. No. (ii)")]
    [InlineData("2026-06-28", "insurance-fund-inspection", ChargeScheme.DepositLinkedInsurance, ChargedEstablishments.Exempted, "0.005", "1250", InsuranceInspectionWords, "S.O. 2701(E), 29 May 2026, under section 143(6) of the Code on Social Security, 2020, Table, Sl. No. (ii)")]
    [InlineData("2026-06-29", "provident-fund-administrative", ChargeScheme.ProvidentFunds, ChargedEstablishments.OtherEstablishments, "0.5", "500", FixingUnder2026SchemeWords, AdministrativeChargesContinuedSource)]
    [InlineData("2026-06-29", "provident-fund-administrative-non-functional", ChargeScheme.ProvidentFunds, ChargedEstablishments.NonFunctionalWithoutMembers, "0.5", "75", FixingUnder2026SchemeWords, AdministrativeChargesContinuedSource)]
    [InlineData("2026-06-29", "provident-fund-inspection", ChargeScheme.ProvidentFunds, ChargedEstablishments.Exempted, "0.18", "8750", "The employer shall pay inspection charges at the rate notified by the Central Government.", "read as the Employees' Provident Funds Scheme, 2026 that superseded it, whose paragraph 13(5) requires the inspection charges the Central Government notifies")]
    [InlineData("2026-06-29", "insurance-fund-inspection", ChargeScheme.DepositLinkedInsurance, ChargedEstablishments.Exempted, "0.005", "1250", "shall pay inspection charges at the rates the Central Government may notify;", "read as the Employees' Deposit-Linked Insurance Scheme, 2026 that superseded it, whose paragraph 27(xii) requires the inspection charges the Central Government notifies")]
    public async Task Charge_OnTheFirstDayOfItsVersionAndTheDayBeforeTheNext_IsTheRateAndLeastSumItsNotificationFixes(string day, string charge, ChargeScheme scheme, ChargedEstablishments establishments, string rate, string minimum, string words, string instrument)
    {
        var date = Day(day);
        await using var scope = factory.Services.CreateAsyncScope();

        var charges = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<ProvidentFundCharges>(date, TestContext.Current.CancellationToken))!.Charges;

        var found = Assert.Single(charges, candidate => candidate.Charge == charge);
        Assert.Equal((scheme, establishments), (found.Scheme, found.Establishments));
        Assert.Equal((Rate(rate), Amount(minimum)), (found.Rate, found.Minimum));
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.Contains(instrument, await SourceAsync(scope, ChargesKey, charge, date), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-04-01", "provident-fund", 15, DueDayCounting.AfterTheMonthCloses, "He shall within fifteen days of the close of every month pay the same to the fund", "Employees' Provident Funds Scheme, 1952, paragraph 38(1), in EPFO's consolidated text")]
    [InlineData("2026-05-28", "provident-fund", 15, DueDayCounting.AfterTheMonthCloses, "He shall within fifteen days of the close of every month pay the same to the fund", "Employees' Provident Funds Scheme, 1952, paragraph 38(1), in EPFO's consolidated text")]
    [InlineData("2026-05-29", "provident-fund", 15, DueDayCounting.AfterTheMonthCloses, "He shall within fifteen days of the close of every month pay the same to the fund", "Employees' Provident Funds Scheme, 1952, paragraph 38(1), in EPFO's consolidated text")]
    [InlineData("2026-06-28", "provident-fund", 15, DueDayCounting.AfterTheMonthCloses, "He shall within fifteen days of the close of every month pay the same to the fund", "Employees' Provident Funds Scheme, 1952, paragraph 38(1), in EPFO's consolidated text")]
    [InlineData("2026-04-01", "insurance-fund", 15, DueDayCounting.AfterTheMonthCloses, "to the Insurance Fund within fifteen days of the close of every month", "Employees' Deposit-Linked Insurance Scheme, 1976, paragraph 8(1), in EPFO's consolidated text")]
    [InlineData("2026-05-28", "insurance-fund", 15, DueDayCounting.AfterTheMonthCloses, "to the Insurance Fund within fifteen days of the close of every month", "Employees' Deposit-Linked Insurance Scheme, 1976, paragraph 8(1), in EPFO's consolidated text")]
    [InlineData("2026-05-29", "insurance-fund", 15, DueDayCounting.AfterTheMonthCloses, "to the Insurance Fund within fifteen days of the close of every month", "Employees' Deposit-Linked Insurance Scheme, 1976, paragraph 8(1), in EPFO's consolidated text")]
    [InlineData("2026-06-28", "insurance-fund", 15, DueDayCounting.AfterTheMonthCloses, "to the Insurance Fund within fifteen days of the close of every month", "Employees' Deposit-Linked Insurance Scheme, 1976, paragraph 8(1), in EPFO's consolidated text")]
    [InlineData("2026-05-29", "inspection-charges", 15, DueDayCounting.AfterTheMonthCloses, "to pay inspection charges … within fifteen days of the close of every month.", "S.O. 2701(E), 29 May 2026, under section 143(6) of the Code on Social Security, 2020")]
    [InlineData("2026-06-28", "inspection-charges", 15, DueDayCounting.AfterTheMonthCloses, "to pay inspection charges … within fifteen days of the close of every month.", "S.O. 2701(E), 29 May 2026, under section 143(6) of the Code on Social Security, 2020")]
    [InlineData("2026-06-29", "inspection-charges", 15, DueDayCounting.AfterTheMonthCloses, "to pay inspection charges … within fifteen days of the close of every month.", "S.O. 2701(E), 29 May 2026, under section 143(6) of the Code on Social Security, 2020")]
    [InlineData("2026-06-29", "provident-fund", 15, DueDayCounting.AfterTheMonthCloses, "The employer shall, within fifteen days of the close of every month, remit the contributions to the Fund", "Employees' Provident Funds Scheme, 2026, paragraphs 20(1) and 28(3)")]
    [InlineData("2026-06-29", "pension-fund", 15, DueDayCounting.AfterTheMonthCloses, "shall be remitted by the employer to the Pension Fund within fifteen days of the close of every month", "Employees' Pension Scheme, 2026, paragraph 4(1)")]
    [InlineData("2026-06-29", "insurance-fund", 15, DueDayCounting.AfterTheMonthCloses, "The employer shall, within fifteen days of the close of every month pay the contribution and administrative charges to the Insurance Fund", "Employees' Deposit-Linked Insurance Scheme, 2026, paragraph 6(1)")]
    [InlineData("2026-06-29", "exempted-fund-to-trustees", 15, DueDayCounting.DayOfTheFollowingMonth, "to the board of trustees by the 15th of each month following the month for which contributions are due", "Employees' Provident Funds Scheme, 2026, paragraph 13(7)")]
    public async Task DueDate_OfEachRemittanceOnTheFirstDayOfItsVersionAndTheDayBeforeTheNext_IsTheDayItsInstrumentSets(string day, string remittance, int days, DueDayCounting counted, string words, string instrument)
    {
        var date = Day(day);
        await using var scope = factory.Services.CreateAsyncScope();

        var remittances = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<ProvidentFundDueDates>(date, TestContext.Current.CancellationToken))!.Remittances;

        var found = Assert.Single(remittances, candidate => candidate.Remittance == remittance);
        Assert.Equal((days, counted), (found.Days, found.Counted));
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.Contains(instrument, await SourceAsync(scope, DueDatesKey, remittance, date), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-04-01", "provident-fund", RoundingMethod.NearestRupeeFiftyPaiseUp, "Each contribution shall be calculated to the nearest rupee, 50 paise or more to be counted as the next higher rupee", "Employees' Provident Funds Scheme, 1952, paragraph 29, in EPFO's consolidated text")]
    [InlineData("2026-06-28", "provident-fund", RoundingMethod.NearestRupeeFiftyPaiseUp, "Each contribution shall be calculated to the nearest rupee, 50 paise or more to be counted as the next higher rupee", "Employees' Provident Funds Scheme, 1952, paragraph 29, in EPFO's consolidated text")]
    [InlineData("2026-04-01", "pension-fund", RoundingMethod.NearestRupee, "Each contribution payable under sub-paragraphs (1) and (2) shall be calculated to the nearest rupee", "Employees' Pension Scheme, 1995, paragraph 3(3), printed as 4(3) in EPFO's consolidated text")]
    [InlineData("2026-06-28", "pension-fund", RoundingMethod.NearestRupee, "Each contribution payable under sub-paragraphs (1) and (2) shall be calculated to the nearest rupee", "Employees' Pension Scheme, 1995, paragraph 3(3), printed as 4(3) in EPFO's consolidated text")]
    [InlineData("2026-04-01", "insurance-fund", RoundingMethod.NearestRupeeFiftyPaiseUp, "50 paise or more to be counted as the next higher rupee and fraction of a rupee less than 50 paise to be ignored.", "Employees' Deposit-Linked Insurance Scheme, 1976, paragraph 7(2), in EPFO's consolidated text")]
    [InlineData("2026-06-28", "insurance-fund", RoundingMethod.NearestRupeeFiftyPaiseUp, "50 paise or more to be counted as the next higher rupee and fraction of a rupee less than 50 paise to be ignored.", "Employees' Deposit-Linked Insurance Scheme, 1976, paragraph 7(2), in EPFO's consolidated text")]
    [InlineData("2026-06-29", "provident-fund", RoundingMethod.NearestRupeeFiftyPaiseUp, "with fifty paise or more to be counted as the next higher rupee and fraction of a rupee less than fifty paise to be ignored.", "Employees' Provident Funds Scheme, 2026, paragraph 18(5)")]
    [InlineData("2026-06-29", "pension-fund", RoundingMethod.NearestRupeeFiftyPaiseUp, "fifty paise or more to be counted as the next higher rupee and fraction of a rupee less than fifty paise being ignored.", "Employees' Pension Scheme, 2026, paragraph 4(3)")]
    [InlineData("2026-06-29", "insurance-fund", RoundingMethod.NearestRupeeFiftyPaiseUp, "fifty paise or more to be counted as the next higher rupee and fraction of a rupee less than fifty paise to be ignored.", "Employees' Deposit-Linked Insurance Scheme, 2026, paragraph 5(3)")]
    public async Task Rounding_OfEachFundsContributionsOnTheFirstDayOfItsVersionAndTheDayBeforeTheNext_IsTheMethodItsSchemeSets(string day, string contributions, RoundingMethod method, string words, string instrument)
    {
        var date = Day(day);
        await using var scope = factory.Services.CreateAsyncScope();

        var roundings = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<ProvidentFundRounding>(date, TestContext.Current.CancellationToken))!.Contributions;

        var found = Assert.Single(roundings, candidate => candidate.Contributions == contributions);
        Assert.Equal(method, found.Method);
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.Contains(instrument, await SourceAsync(scope, RoundingKey, contributions, date), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RatesKey, "2026-04-01", "pension-fund-central-government,pension-fund-employer,pension-fund-employer-joint-option,provident-fund-employee,provident-fund-employer,provident-fund-employer-ten-per-cent")]
    [InlineData(RatesKey, "2026-06-28", "pension-fund-central-government,pension-fund-employer,pension-fund-employer-joint-option,provident-fund-employee,provident-fund-employer,provident-fund-employer-ten-per-cent")]
    [InlineData(RatesKey, "2026-06-29", "insurance-fund-employer,pension-fund-central-government,pension-fund-employer,pension-fund-employer-joint-option,provident-fund-employee,provident-fund-employer,provident-fund-employer-ten-per-cent")]
    [InlineData(RatesKey, "2026-09-16", "insurance-fund-employer,pension-fund-central-government,pension-fund-employer,pension-fund-employer-joint-option,provident-fund-employee,provident-fund-employer,provident-fund-employer-ten-per-cent")]
    [InlineData(RatesKey, "2026-09-17", "insurance-fund-employer,pension-fund-central-government,pension-fund-employer,pension-fund-employer-joint-option,provident-fund-employee,provident-fund-employer,provident-fund-employer-ten-per-cent")]
    [InlineData(EstablishmentsKey, "2026-04-01", "2(i),2(ii)(a),2(ii)(b),2(ii)(c),2(ii)(d),2(ii)(e)")]
    [InlineData(EstablishmentsKey, "2026-11-08", "2(i),2(ii)(a),2(ii)(b),2(ii)(c),2(ii)(d),2(ii)(e)")]
    [InlineData(LimitsKey, "2026-04-01", "excluded-employee,excluded-employee-with-disability,pension-scheme-member")]
    [InlineData(LimitsKey, "2026-05-28", "excluded-employee,excluded-employee-with-disability,pension-scheme-member")]
    [InlineData(LimitsKey, "2026-05-29", "excluded-employee,excluded-employee-with-disability,pension-scheme-member,wage-ceiling")]
    [InlineData(LimitsKey, "2026-06-28", "excluded-employee,excluded-employee-with-disability,pension-scheme-member,wage-ceiling")]
    [InlineData(LimitsKey, "2026-06-29", "excluded-employee,pension-scheme-member,wage-ceiling")]
    [InlineData(LimitsKey, "2026-09-16", "excluded-employee,pension-scheme-member,wage-ceiling")]
    [InlineData(LimitsKey, "2026-09-17", "excluded-employee,pension-scheme-member,pension-scheme-member-on-new-ceiling,wage-ceiling")]
    [InlineData(ChargesKey, "2026-04-01", "provident-fund-administrative,provident-fund-administrative-non-functional")]
    [InlineData(ChargesKey, "2026-05-28", "provident-fund-administrative,provident-fund-administrative-non-functional")]
    [InlineData(ChargesKey, "2026-05-29", "insurance-fund-inspection,provident-fund-administrative,provident-fund-administrative-non-functional,provident-fund-inspection")]
    [InlineData(ChargesKey, "2026-06-28", "insurance-fund-inspection,provident-fund-administrative,provident-fund-administrative-non-functional,provident-fund-inspection")]
    [InlineData(ChargesKey, "2026-06-29", "insurance-fund-inspection,provident-fund-administrative,provident-fund-administrative-non-functional,provident-fund-inspection")]
    [InlineData(DueDatesKey, "2026-04-01", "insurance-fund,provident-fund")]
    [InlineData(DueDatesKey, "2026-05-28", "insurance-fund,provident-fund")]
    [InlineData(DueDatesKey, "2026-05-29", "inspection-charges,insurance-fund,provident-fund")]
    [InlineData(DueDatesKey, "2026-06-28", "inspection-charges,insurance-fund,provident-fund")]
    [InlineData(DueDatesKey, "2026-06-29", "exempted-fund-to-trustees,inspection-charges,insurance-fund,pension-fund,provident-fund")]
    [InlineData(RoundingKey, "2026-04-01", "insurance-fund,pension-fund,provident-fund")]
    [InlineData(RoundingKey, "2026-06-28", "insurance-fund,pension-fund,provident-fund")]
    [InlineData(RoundingKey, "2026-06-29", "insurance-fund,pension-fund,provident-fund")]
    public async Task Entries_OfEachSetOnTheFirstDayOfEachVersionAndTheDayBeforeTheNext_AreTheEntriesInForce(string key, string day, string entries)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, Day(day), TestContext.Current.CancellationToken);

        Assert.NotNull(view);
        Assert.Equal(entries.Split(','), view.Rows.Select(row => row.Key).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(RatesKey, "2026-04-01,2026-06-29,2026-09-17")]
    [InlineData(EstablishmentsKey, "2026-04-01")]
    [InlineData(LimitsKey, "2026-04-01,2026-05-29,2026-06-29,2026-09-17")]
    [InlineData(ChargesKey, "2026-04-01,2026-05-29,2026-06-29")]
    [InlineData(DueDatesKey, "2026-04-01,2026-05-29,2026-06-29")]
    [InlineData(RoundingKey, "2026-04-01,2026-06-29")]
    public async Task Versions_OfEachSet_AreTheDaysItsInstrumentsCameIntoForceFrom1April2026(string key, string dates)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, Today, TestContext.Current.CancellationToken);

        Assert.NotNull(set);
        Assert.Equal((ParameterSetBasis.Law, ParameterAsOf.WageDate), (set.Basis, set.AsOf));
        Assert.Equal(dates.Split(',').Select(Day).Reverse(), set.Versions.Select(version => version.From));
    }

    [Theory]
    [InlineData(RatesKey, "2026-04-01", 6, 0, 0, new[] { CodeOnRatesAndContinuation, CodesCommencement, TenPerCentNotification, ContinuedRateSchemes })]
    [InlineData(RatesKey, "2026-06-29", 1, 6, 0, new[] { ProvidentFundsScheme2026, PensionScheme2026, InsuranceScheme2026, PensionRateNotification, InsuranceRateNotification, FirstWageCeiling })]
    [InlineData(RatesKey, "2026-09-17", 0, 6, 0, new[] { SecondWageCeiling, CentralGovernmentLimit })]
    [InlineData(EstablishmentsKey, "2026-04-01", 6, 0, 0, new[] { TenPerCentNotification, CodeOnTheTenPerCent })]
    [InlineData(LimitsKey, "2026-04-01", 3, 0, 0, new[] { ContinuedMembershipSchemes, CodeOnContinuation })]
    [InlineData(LimitsKey, "2026-05-29", 1, 0, 0, new[] { FirstWageCeiling })]
    [InlineData(LimitsKey, "2026-06-29", 0, 2, 1, new[] { ProvidentFundsScheme2026, PensionScheme2026 })]
    [InlineData(LimitsKey, "2026-09-17", 1, 3, 0, new[] { SecondWageCeiling, NewCeilingMembership })]
    [InlineData(ChargesKey, "2026-04-01", 2, 0, 0, new[] { AdministrativeCharges, CodeOnContinuation })]
    [InlineData(ChargesKey, "2026-05-29", 2, 0, 0, new[] { InspectionCharges, InspectionCorrigendum })]
    [InlineData(ChargesKey, "2026-06-29", 0, 4, 0, new[] { ProvidentFundsScheme2026, InsuranceScheme2026, NotificationsContinued })]
    [InlineData(DueDatesKey, "2026-04-01", 2, 0, 0, new[] { ContinuedDueDateSchemes, CodeOnContinuation })]
    [InlineData(DueDatesKey, "2026-05-29", 1, 0, 0, new[] { InspectionCharges })]
    [InlineData(DueDatesKey, "2026-06-29", 2, 2, 0, new[] { ProvidentFundsScheme2026, PensionScheme2026, InsuranceScheme2026 })]
    [InlineData(RoundingKey, "2026-04-01", 3, 0, 0, new[] { ContinuedRoundingSchemes, CodeOnContinuation })]
    [InlineData(RoundingKey, "2026-06-29", 0, 3, 0, new[] { ProvidentFundsScheme2026, PensionScheme2026, InsuranceScheme2026 })]
    public async Task Version_OfEachSet_CitesItsInstrumentsInOrderAndChangesTheEntriesTheyChange(string key, string from, int added, int changed, int removed, string[] instruments)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, Today, TestContext.Current.CancellationToken);

        var version = Assert.Single(set!.Versions, candidate => candidate.From == Day(from));
        Assert.Equal(instruments.Length, version.Instruments.Count);
        Assert.All(instruments.Zip(version.Instruments), pair => Assert.StartsWith(pair.First, pair.Second, StringComparison.Ordinal));
        Assert.Equal((added, changed, removed), (version.AddedCount, version.ChangedCount, version.RemovedCount));
    }

    [Theory]
    [InlineData(RatesKey, "2026-04-01", TenPerCentNotification, "“This notification shall be deemed to come into force from the 21st day of November, 2025.”")]
    [InlineData(EstablishmentsKey, "2026-04-01", TenPerCentNotification, "“This notification shall be deemed to come into force from the 21st day of November, 2025.”")]
    [InlineData(RatesKey, "2026-06-29", ProvidentFundsScheme2026, "“It shall come into force on the date of its publication in the Official Gazette.”; corrected by G.S.R. 703(E), 4 August 2026, No. 639")]
    [InlineData(RatesKey, "2026-06-29", PensionScheme2026, "“It shall come into force on the date of its publication in the Official Gazette.”; corrected by G.S.R. 704(E), 4 August 2026, No. 640")]
    [InlineData(RatesKey, "2026-06-29", InsuranceScheme2026, "“It shall come into force on the date of its publication in the Official Gazette.”")]
    [InlineData(RatesKey, "2026-06-29", PensionRateNotification, "“with effect from the commencement of Employees' Pension Scheme, 2026 i.e. 29th June, 2026.”")]
    [InlineData(RatesKey, "2026-06-29", InsuranceRateNotification, "“with effect from the commencement of Employees' Deposit Linked Insurance Scheme, 2026 i.e. 29th June, 2026.”")]
    [InlineData(RatesKey, "2026-09-17", SecondWageCeiling, "“with effect from the date of publication of this notification in the Official Gazette.”")]
    [InlineData(RatesKey, "2026-09-17", CentralGovernmentLimit, "“This notification shall come into force on the 17th September, 2026.”")]
    [InlineData(LimitsKey, "2026-05-29", FirstWageCeiling, "in force on its publication, the notification naming no other day")]
    [InlineData(LimitsKey, "2026-09-17", NewCeilingMembership, "“This notification shall come into force on the 17th September, 2026.”")]
    [InlineData(ChargesKey, "2026-04-01", AdministrativeCharges, "“with effect from 1st June, 2018”")]
    [InlineData(ChargesKey, "2026-05-29", InspectionCharges, "in force on its publication, the notification naming no other day")]
    [InlineData(ChargesKey, "2026-05-29", InspectionCorrigendum, "for \"0.35\" read \"0.18\"")]
    public async Task Version_WhoseInstrumentDatesItsOwnForce_QuotesTheWordsThatDateIt(string key, string from, string instrument, string words)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, Today, TestContext.Current.CancellationToken);

        var version = Assert.Single(set!.Versions, candidate => candidate.From == Day(from));
        Assert.Contains(words, Assert.Single(version.Instruments, cited => cited.StartsWith(instrument, StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sets_OnTheDayBefore1April2026_HoldNothing()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();
        var day = FirstDay.AddDays(-1);
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Null(await store.GetAsOfAsync<ProvidentFundContributionRates>(day, cancellationToken));
        Assert.Null(await store.GetAsOfAsync<TenPerCentEstablishments>(day, cancellationToken));
        Assert.Null(await store.GetAsOfAsync<ProvidentFundMembershipLimits>(day, cancellationToken));
        Assert.Null(await store.GetAsOfAsync<ProvidentFundCharges>(day, cancellationToken));
        Assert.Null(await store.GetAsOfAsync<ProvidentFundDueDates>(day, cancellationToken));
        Assert.Null(await store.GetAsOfAsync<ProvidentFundRounding>(day, cancellationToken));
    }

    private static DateOnly Day(string value) => DateOnly.Parse(value, CultureInfo.InvariantCulture);

    private static Percentage Rate(string value) => new(decimal.Parse(value, CultureInfo.InvariantCulture));

    private static Percentage? Percent(string? value) => value is null ? null : Rate(value);

    private static Money Amount(string value) => new(decimal.Parse(value, CultureInfo.InvariantCulture), Currency.Inr);

    private static Money? Rupees(string? value) => value is null ? null : Amount(value);

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string key, string rowKey, DateOnly day)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, day, TestContext.Current.CancellationToken);

        return Assert.Single(view!.Rows, row => row.Key == rowKey).Source;
    }
}
