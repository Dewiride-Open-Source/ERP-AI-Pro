using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProfessionalTax;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Labour;

// Golden values of the professional tax on salary and wage earners from 1 April 2026: which States levy it and under which Act,
// every slab with its bounds and tax, and the exemptions, each pinned beside its instrument so that a wrong edit of a seed file
// fails a test (ADR-0063). West Bengal's slabs are pinned on the last day before the West Bengal Finance Act, 2026 ends their basis
// on 1 October 2026 and are absent from that day, while its levy stays in force.
[Collection(StatutoryRows.Name)]
public sealed class ProfessionalTaxGoldenTests(ErpApiFactory factory)
{
    private const string LeviesKey = "professional-tax.levies";

    private const string SlabsKey = "professional-tax.salary-slabs";

    private const string ExemptionsKey = "professional-tax.exemptions";

    private const string NagalandEarners = "Salary and wages earners: Such persons whose monthly salaries or wages are-";

    private const string MizoramEarners = "Salary and Wage earners where the total gross monthly income -";

    private const string TripuraEarners = "Salary & Wage earners:- When the range of monthly gross income is-";

    private const string AssamEarners = "Salary and wage earners, such persons whose monthly salaries or wages are-";

    private const string WestBengalEarners = "Employees earning monthly salary or wages—";

    private const string GujaratGovernmentEarners = "Salary and Wages earners of the State Government, Central Government, Panchayats, Public Sector Undertakings of the State and the Central Government, and Grant-in-aids institutions";

    private const string GujaratOtherEarners = "Salary and Wages earners other than those mentioned in sub-entry(A)";

    private const string MaharashtraMen = "Salary and wage earners— (i) in case of men, whose monthly salaries or wages,—";

    private const string MaharashtraWomen = "Salary and wage earners— (ii) in case of women, whose monthly salaries or wages,—";

    private const string TelanganaEarners = "Salary and wage earners whose monthly salaries or wages in Rs:";

    private const string WestBengalSchedule = "in the Directorate of Commercial Taxes' Schedule effective from 1 April 2014";

    private const string MaharashtraEntry = "as substituted by Mah. Act No. XIX of 2023, section 3";

    private const string KarnatakaNotification = "Notification-2, No. FD 09 CPT 2023, Finance Department, Karnataka, 13 July 2023";

    private const string PunjabExtent = "It extends to the whole of the State of Punjab, and offices of the Government of Punjab and offices of any body, whether incorporated or not, which is owned or controlled by the State of Punjab situated in the capital of Punjab.";

    private const string MizoramExtent = "It shall extent to the whole of Mizoram excluding the areas of Lai, Mara and Chakma Autonomous District Councils in Chhimtuipui District.";

    private const string GujaratExplanations = "[Explanation I] … the monthly salary or wages shall, for the purpose of this entry, be reckoned on the basis of the actual amount of salary or wages paid or payable for a month [Explanation II] Where a person ceases to be a salary or wages earner before the end of any month, his liability to pay the tax for that month shall be proportionately reduced.";

    private static readonly DateOnly WindowOpens = new(2026, 4, 1);

    private static readonly DateOnly WestBengalBasisEnds = new(2026, 10, 1);

    [Theory]
    [InlineData("03-1", "03", null, null, null, null, null, "200", null, null, null, null)]
    [InlineData("10-1(a)", "10", ProfessionalTaxSlabMeasure.AnnualIncome, null, null, "300000", null, "0", "0", null, null, null)]
    [InlineData("10-1(b)", "10", ProfessionalTaxSlabMeasure.AnnualIncome, "300000", null, "500000", null, null, "1000", null, null, null)]
    [InlineData("10-1(c)", "10", ProfessionalTaxSlabMeasure.AnnualIncome, "500000", null, "1000000", null, null, "2000", null, null, null)]
    [InlineData("10-1(d)", "10", ProfessionalTaxSlabMeasure.AnnualIncome, "1000000", null, null, null, null, "2500", null, null, null)]
    [InlineData("13-1(i)", "13", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, null, null, null, "4000", "0", "0", null, null, null)]
    [InlineData("13-1(ii)", "13", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, "4000", null, null, "5000", "35", null, null, null, null)]
    [InlineData("13-1(iii)", "13", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, "5000", null, null, "7000", "75", null, null, null, null)]
    [InlineData("13-1(iv)", "13", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, "7000", null, null, "9000", "110", null, null, null, null)]
    [InlineData("13-1(v)", "13", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, "9000", null, null, "12000", "180", null, null, null, null)]
    [InlineData("13-1(vi)", "13", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, null, "12000", null, null, "208", null, null, null, null)]
    [InlineData("15-1(1)", "15", ProfessionalTaxSlabMeasure.MonthlyGrossIncome, null, null, "5000", null, "0", "0", null, null, null)]
    [InlineData("15-1(2)", "15", ProfessionalTaxSlabMeasure.MonthlyGrossIncome, "5000", null, "8000", null, "75", null, null, null, "900")]
    [InlineData("15-1(3)", "15", ProfessionalTaxSlabMeasure.MonthlyGrossIncome, "8000", null, "10000", null, "120", null, null, null, "1440")]
    [InlineData("15-1(4)", "15", ProfessionalTaxSlabMeasure.MonthlyGrossIncome, "10000", null, "12000", null, "150", null, null, null, "1800")]
    [InlineData("15-1(5)", "15", ProfessionalTaxSlabMeasure.MonthlyGrossIncome, "12000", null, "15000", null, "180", null, null, null, "2160")]
    [InlineData("15-1(6)", "15", ProfessionalTaxSlabMeasure.MonthlyGrossIncome, "15000", null, "20000", null, "195", null, null, null, "2340")]
    [InlineData("15-1(7)", "15", ProfessionalTaxSlabMeasure.MonthlyGrossIncome, "20000", null, null, null, "208", null, null, null, "2500")]
    [InlineData("16-1(i)", "16", ProfessionalTaxSlabMeasure.MonthlyGrossIncome, null, null, "7500", null, "0", "0", null, null, null)]
    [InlineData("16-1(ii)", "16", ProfessionalTaxSlabMeasure.MonthlyGrossIncome, null, "7501", "15000", null, "150", "1800", null, null, null)]
    [InlineData("16-1(iii)", "16", ProfessionalTaxSlabMeasure.MonthlyGrossIncome, null, "15001", null, null, "208", "2496", null, null, null)]
    [InlineData("18-1(i)", "18", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, null, null, "15000", null, "0", "0", null, null, null)]
    [InlineData("18-1(ii)", "18", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, null, "15001", null, "25000", "180", null, null, null, null)]
    [InlineData("18-1(iii)", "18", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, null, "25000", null, null, "208", null, null, null, null)]
    [InlineData("19-1(i)", "19", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, null, null, "10000", null, "0", "0", null, null, null)]
    [InlineData("19-1(iii)", "19", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, "10000", null, "15000", null, "110", null, null, null, null)]
    [InlineData("19-1(iv)", "19", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, "15000", null, "25000", null, "130", null, null, null, null)]
    [InlineData("19-1(v)", "19", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, "25000", null, "40000", null, "150", null, null, null, null)]
    [InlineData("19-1(vi)", "19", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, "40000", null, null, null, "200", null, null, null, null)]
    [InlineData("24-1(A)(i)", "24", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, null, null, "12000", null, "0", "0", null, null, null)]
    [InlineData("24-1(A)(ii)", "24", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, "12000", null, null, null, "200", null, null, null, null)]
    [InlineData("24-1(B)(i)", "24", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, null, null, "12000", null, "0", "0", null, null, null)]
    [InlineData("24-1(B)(ii)", "24", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, "12000", null, null, null, "200", null, null, null, null)]
    [InlineData("27-1(i)(a)", "27", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, null, null, "7500", null, "0", "0", null, null, null)]
    [InlineData("27-1(i)(b)", "27", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, "7500", null, "10000", null, "175", null, null, null, null)]
    [InlineData("27-1(i)(c)", "27", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, "10000", null, null, null, "200", "2500", 2, "300", null)]
    [InlineData("27-1(ii)(a)", "27", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, null, null, "25000", null, "0", "0", null, null, null)]
    [InlineData("27-1(ii)(b)", "27", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, "25000", null, null, null, "200", "2500", 2, "300", null)]
    [InlineData("29-1", "29", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, null, "25000", null, null, "200", null, 2, "300", null)]
    [InlineData("36-1(i)", "36", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, null, null, "15000", null, "0", "0", null, null, null)]
    [InlineData("36-1(ii)", "36", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, null, "15001", "20000", null, "150", null, null, null, null)]
    [InlineData("36-1(iii)", "36", ProfessionalTaxSlabMeasure.MonthlySalaryOrWages, "20000", null, null, null, "200", null, null, null, null)]
    public async Task Slab_OnTheDayTheWindowOpens_HasTheBoundsAndTaxItsEntryPrints(string slab, string stateCode, ProfessionalTaxSlabMeasure? measure, string? above, string? atLeast, string? upTo, string? below, string? monthly, string? yearly, int? otherMonth, string? otherMonthTax, string? lumpSum)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var slabs = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<ProfessionalTaxSlabs>(WindowOpens, TestContext.Current.CancellationToken))!.Slabs;

        var found = Assert.Single(slabs, candidate => candidate.Slab == slab);
        Assert.Equal((stateCode, measure), (found.StateCode, found.Measure));
        Assert.Equal((RupeesOf(above), RupeesOf(atLeast), RupeesOf(upTo), RupeesOf(below)), (found.Above, found.AtLeast, found.UpTo, found.Below));
        Assert.Equal((RupeesOf(monthly), RupeesOf(yearly), otherMonth, RupeesOf(otherMonthTax), RupeesOf(lumpSum)), (found.Monthly, found.Yearly, found.OtherMonth, found.OtherMonthTax, found.LumpSum));
    }

    [Theory]
    [InlineData("03-1", "All such persons who are assessable under the Head Income from Salaries and/ or Wages as per the Income Tax Act, 1961.", null, "Rs. 200/- per month", "Punjab State Development Tax Act, 2018, Schedule, serial number 1")]
    [InlineData("10-1(a)", null, "(a) Persons, whose income do not exceed three lac rupees per annum.", "Nil", "Schedule, serial number 1(a), as substituted by S.O. 28, Commercial Taxes Department, 2 February 2016")]
    [InlineData("10-1(b)", null, "(b) Persons, whose income exceeds three lac rupees per annum but does not exceed five lac rupees per annum.", "One thousand rupees per annum.", "Schedule, serial number 1(b), as substituted by S.O. 28, Commercial Taxes Department, 2 February 2016")]
    [InlineData("10-1(c)", null, "(c) Persons, whose income exceeds five lac rupees per annum but does not exceed ten lac rupees per annum.", "Two thousand rupees per annum", "Schedule, serial number 1(c), as substituted by S.O. 28, Commercial Taxes Department, 2 February 2016")]
    [InlineData("10-1(d)", null, "(d) Persons, whose income exceeds ten lac rupees per annum.", "Two thousand and five hundred rupees per annum", "Schedule, serial number 1(d), as substituted by S.O. 28, Commercial Taxes Department, 2 February 2016")]
    [InlineData("13-1(i)", NagalandEarners, "(i) Less than Rs 4000/", "(i) Nil", "the Schedule, serial 1(i), as published by the Department of Taxes, Nagaland")]
    [InlineData("13-1(ii)", NagalandEarners, "(ii) More than Rs 4000 but less than Rs 5000", "(ii) 35 per mensem", "the Schedule, serial 1(ii), as published by the Department of Taxes, Nagaland")]
    [InlineData("13-1(iii)", NagalandEarners, "(iii) More than Rs 5000 but less than Rs 7000", "(iii) 75 per mensem", "the Schedule, serial 1(iii), as published by the Department of Taxes, Nagaland")]
    [InlineData("13-1(iv)", NagalandEarners, "(iv) More than Rs 7000 but less than Rs 9000", "(iv) 110 per mensem", "the Schedule, serial 1(iv), as published by the Department of Taxes, Nagaland")]
    [InlineData("13-1(v)", NagalandEarners, "(v) More than Rs 9000 but less than Rs 12000", "(v) 180 per mensem", "the Schedule, serial 1(v), as published by the Department of Taxes, Nagaland")]
    [InlineData("13-1(vi)", NagalandEarners, "(vi) Rs 12000 or more", "(vi) 208 per mensem", "the Schedule, serial 1(vi), as published by the Department of Taxes, Nagaland")]
    [InlineData("15-1(1)", MizoramEarners, "(1) Does not exceed 5,000/-", "NIL", "Notification No. J.17011/1/2017-TAX, Taxation Department, Mizoram, 11 July 2024, the Schedule, serial number 1(1)")]
    [InlineData("15-1(2)", MizoramEarners, "(2) Exceeds 5,000/- but does not exceed 8,000.00", "75.00 per month or the assessee may pay 900.00 per annum in lump-sum.", "Notification No. J.17011/1/2017-TAX, Taxation Department, Mizoram, 11 July 2024, the Schedule, serial number 1(2)")]
    [InlineData("15-1(3)", MizoramEarners, "(3) Exceeds 8,000.00 but does not exceed 10,000.00", "120.00 per month or the assessee may pay 1,440.00 per annum in lump-sum.", "Notification No. J.17011/1/2017-TAX, Taxation Department, Mizoram, 11 July 2024, the Schedule, serial number 1(3)")]
    [InlineData("15-1(4)", MizoramEarners, "(4) Exceeds 10,000.00 but does not exceed 12,000.00", "150.00 per month or the assessee may pay Rs. 1,800.00 per annum in lump-sum.", "Notification No. J.17011/1/2017-TAX, Taxation Department, Mizoram, 11 July 2024, the Schedule, serial number 1(4)")]
    [InlineData("15-1(5)", MizoramEarners, "(5) Exceeds 12,000.00 but does not exceed 15,000.00", "180.00 per month or the assessee may pay 2,160.00 per annum in lump-sum.", "Notification No. J.17011/1/2017-TAX, Taxation Department, Mizoram, 11 July 2024, the Schedule, serial number 1(5)")]
    [InlineData("15-1(6)", MizoramEarners, "(6) Exceeds 15,000.00 but does not exceed 20,000.00", "195.00 per month or the assessee may pay Rs. 2,340.00 per annum in lump-sum.", "Notification No. J.17011/1/2017-TAX, Taxation Department, Mizoram, 11 July 2024, the Schedule, serial number 1(6)")]
    [InlineData("15-1(7)", MizoramEarners, "(7) Exceeds 20,000.00", "208.00 per month or the assessee may pay Rs. 2,500.00 per annum in lump-sum.", "Notification No. J.17011/1/2017-TAX, Taxation Department, Mizoram, 11 July 2024, the Schedule, serial number 1(7)")]
    [InlineData("16-1(i)", TripuraEarners, "(i) Up to Rs. 7,500/-", "Nil", "Finance Department (Taxes & Excise), Tripura, 25 July 2018, Schedule, serial number 1(i)")]
    [InlineData("16-1(ii)", TripuraEarners, "(ii) Rs. 7,501/- and above and up to Rs. 15,000/-", "Rs. 1800/- (Rs. 150/- per month)", "serial number 1(ii), as substituted by the Corrigendum of 30 October 2018, clause (i)")]
    [InlineData("16-1(iii)", TripuraEarners, "(iii) Rs. 15001/- and above", "Rs. 2496/- (Rs. 208/- per month)", "Finance Department (Taxes & Excise), Tripura, 25 July 2018, Schedule, serial number 1(iii)")]
    [InlineData("18-1(i)", AssamEarners, "(i) Up to Rs. 15,000.00", "Nil", "Schedule, entry 1(i), as substituted by Notification eCF No. 169350/124, Finance (Taxation) Department, 1 April 2025")]
    [InlineData("18-1(ii)", AssamEarners, "(ii) Rs.15,001.00 or more but less than Rs.25,000.00", "Rs. 180.00 per month", "Schedule, entry 1(ii), as substituted by Notification eCF No. 169350/124, Finance (Taxation) Department, 1 April 2025")]
    [InlineData("18-1(iii)", AssamEarners, "(iii) Rs. 25,000.00 and more", "Rs. 208.00 per month", "Schedule, entry 1(iii), as substituted by Notification eCF No. 169350/124, Finance (Taxation) Department, 1 April 2025")]
    [InlineData("19-1(i)", WestBengalEarners, "(i) Not exceeding Rs. 10,000", "Nil", "Schedule, serial number 1, item (i), as amended by the West Bengal Finance Act, 2016, section 2(3)(a)")]
    [InlineData("19-1(iii)", WestBengalEarners, "(iii) Above Rs. 10,000 but not exceeding Rs. 15,000", "Rs.110 per month", "Schedule, serial number 1, item (iii), " + WestBengalSchedule)]
    [InlineData("19-1(iv)", WestBengalEarners, "(iv) Above Rs. 15,000 but not exceeding Rs. 25,000", "Rs. 130 per month", "Schedule, serial number 1, item (iv), " + WestBengalSchedule)]
    [InlineData("19-1(v)", WestBengalEarners, "(v) Above Rs.25,000 but not exceeding Rs.40,000", "Rs. 150 per month", "Schedule, serial number 1, item (v), " + WestBengalSchedule)]
    [InlineData("19-1(vi)", WestBengalEarners, "(vi) Above Rs.40,000", "Rs. 200 per month", "Schedule, serial number 1, item (vi), " + WestBengalSchedule)]
    [InlineData("24-1(A)(i)", GujaratGovernmentEarners, "(i) Up to ₹12,000/-", "[Rate of tax per month (in ₹)] (i) Zero", "Notification No. GHN-35-PFT-2022-S.3(2)(10)-Th, Finance Department, Gujarat, 8 April 2022, Schedule, entry 1(A)(i)")]
    [InlineData("24-1(A)(ii)", GujaratGovernmentEarners, "(ii) More than ₹12,000/-", "[Rate of tax per month (in ₹)] (ii) ₹200/-", "Notification No. GHN-35-PFT-2022-S.3(2)(10)-Th, Finance Department, Gujarat, 8 April 2022, Schedule, entry 1(A)(ii)")]
    [InlineData("24-1(B)(i)", GujaratOtherEarners, "(i) Up to ₹12,000/-", "[Rate of tax per month (in ₹)] (i) Zero", "Notification No. GHN-35-PFT-2022-S.3(2)(10)-Th, Finance Department, Gujarat, 8 April 2022, Schedule, entry 1(B)(i)")]
    [InlineData("24-1(B)(ii)", GujaratOtherEarners, "(ii) More than ₹12,000/-", "[Rate of tax per month (in ₹)] (ii) ₹200/-", "Notification No. GHN-35-PFT-2022-S.3(2)(10)-Th, Finance Department, Gujarat, 8 April 2022, Schedule, entry 1(B)(ii)")]
    [InlineData("27-1(i)(a)", MaharashtraMen, "(a) do not exceed rupees seven thousand five hundred ;", "Nil.", "Schedule I, entry 1(i)(a), " + MaharashtraEntry)]
    [InlineData("27-1(i)(b)", MaharashtraMen, "(b) exceed rupees seven thousand five hundred but do not exceed rupees ten thousand ;", "one hundred seventy-five per month.", "Schedule I, entry 1(i)(b), " + MaharashtraEntry)]
    [InlineData("27-1(i)(c)", MaharashtraMen, "(c) exceed rupees ten thousand ;", "two thousand five hundred per annum to be paid in following manner :— (a) two hundred per month except for the month of February ; (b) three hundred for the month of February;", "Schedule I, entry 1(i)(c), " + MaharashtraEntry)]
    [InlineData("27-1(ii)(a)", MaharashtraWomen, "(a) do not exceed rupees twenty-five thousand ;", "Nil.", "Schedule I, entry 1(ii)(a), " + MaharashtraEntry)]
    [InlineData("27-1(ii)(b)", MaharashtraWomen, "(b) exceed rupees twenty-five thousand ;", "two thousand five hundred per annum to be paid in following manner :— (a) two hundred per month except for the month of February ; (b) three hundred for the month of February.", "Schedule I, entry 1(ii)(b), " + MaharashtraEntry)]
    [InlineData("29-1", "Salary or wage earners whose salary or wage or both, as the case may be, for a month is Rs. 25,000-00 and above", null, "(i) Rs.200-00 per month (except for the month of February); (ii) Rs.300-00 for the month of February", "column (2) as substituted by Karnataka Act No. 14 of 2023, section 7, and column (3) as substituted by Karnataka Act No. 33 of 2025, section 2")]
    [InlineData("36-1(i)", TelanganaEarners, "(i) Up to 15,000", "Nil", "First Schedule, entry 1(i), as amended up to date in the consolidated text of the Commercial Taxes Department")]
    [InlineData("36-1(ii)", TelanganaEarners, "(ii) From 15,001 to 20,000", "150 PM", "First Schedule, entry 1(ii), as amended up to date in the consolidated text of the Commercial Taxes Department")]
    [InlineData("36-1(iii)", TelanganaEarners, "(iii) Above 20,000", "200 PM", "First Schedule, entry 1(iii), as amended up to date in the consolidated text of the Commercial Taxes Department")]
    public async Task Slab_OnTheDayTheWindowOpens_IsWordedAsItsEntryPrintsIt(string slab, string? earners, string? band, string rate, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var slabs = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<ProfessionalTaxSlabs>(WindowOpens, TestContext.Current.CancellationToken))!.Slabs;

        var found = Assert.Single(slabs, candidate => candidate.Slab == slab);
        Assert.Equal((earners, band, rate), (found.Earners, found.Band, found.Rate));
        Assert.Contains(instrument, await SourceAsync(scope, SlabsKey, WindowOpens, slab), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("03", 1)]
    [InlineData("10", 4)]
    [InlineData("13", 6)]
    [InlineData("15", 7)]
    [InlineData("16", 3)]
    [InlineData("18", 3)]
    [InlineData("19", 5)]
    [InlineData("24", 4)]
    [InlineData("27", 5)]
    [InlineData("29", 1)]
    [InlineData("36", 3)]
    public async Task Slabs_OfEachStateOnTheDayTheWindowOpens_AreEveryEntryItsInstrumentPrints(string stateCode, int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var slabs = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<ProfessionalTaxSlabs>(WindowOpens, TestContext.Current.CancellationToken))!.Slabs;

        Assert.Equal(count, slabs.Count(slab => slab.StateCode == stateCode));
    }

    [Fact]
    public async Task Slabs_OnTheDayTheWindowOpens_AreForEveryStateTheLeviesSayLeviesTheTaxAndNoOther()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        var slabs = (await store.GetAsOfAsync<ProfessionalTaxSlabs>(WindowOpens, TestContext.Current.CancellationToken))!.Slabs;
        var levies = (await store.GetAsOfAsync<ProfessionalTaxLevies>(WindowOpens, TestContext.Current.CancellationToken))!.States;
        string[] levied = [.. levies.Where(levy => levy.Status == ProfessionalTaxLevyStatus.Levied).Select(levy => levy.StateCode).Order(StringComparer.Ordinal)];

        Assert.Equal(levied, slabs.Select(slab => slab.StateCode).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task GujaratSlabs_OnTheDayTheWindowOpens_CarryTheNotificationsExplanationsAndNoOtherSlabDoes()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var slabs = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<ProfessionalTaxSlabs>(WindowOpens, TestContext.Current.CancellationToken))!.Slabs;

        Assert.Equal(4, slabs.Count(slab => slab.StateCode == "24"));
        Assert.All(slabs.Where(slab => slab.StateCode == "24"), slab => Assert.Equal(GujaratExplanations, slab.Explanations));
        Assert.All(slabs.Where(slab => slab.StateCode != "24"), slab => Assert.Null(slab.Explanations));
    }

    [Fact]
    public async Task WestBengalSlabs_OnTheDayBeforeAndTheDayItsFinanceAct2026EndsTheirBasis_AreInForceThenGoneWhileItsLevyStays()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        var before = await store.GetAsOfAsync<ProfessionalTaxSlabs>(WestBengalBasisEnds.AddDays(-1), TestContext.Current.CancellationToken);
        var onTheDay = await store.GetAsOfAsync<ProfessionalTaxSlabs>(WestBengalBasisEnds, TestContext.Current.CancellationToken);
        var levies = await store.GetAsOfAsync<ProfessionalTaxLevies>(WestBengalBasisEnds, TestContext.Current.CancellationToken);

        Assert.NotNull(before);
        Assert.Equal((WindowOpens, (DateOnly?)WestBengalBasisEnds.AddDays(-1)), (before.Version.From, before.Version.Until));
        Assert.Equal(["19-1(i)", "19-1(iii)", "19-1(iv)", "19-1(v)", "19-1(vi)"], before.Slabs.Where(slab => slab.StateCode == "19").Select(slab => slab.Slab).Order(StringComparer.Ordinal));
        Assert.NotNull(onTheDay);
        Assert.Equal((WestBengalBasisEnds, (DateOnly?)null), (onTheDay.Version.From, onTheDay.Version.Until));
        Assert.DoesNotContain(onTheDay.Slabs, slab => slab.StateCode == "19");
        Assert.Equal(37, onTheDay.Slabs.Count);
        Assert.Equal(ProfessionalTaxLevyStatus.Levied, Assert.Single(levies!.States, levy => levy.StateCode == "19").Status);
    }

    [Theory]
    [InlineData("01", ProfessionalTaxLevyStatus.NotLevied, "Certificate of the Commissioner State Taxes, Jammu and Kashmir, 18 October 2021", "It is to certify that no taxes on professions, trades, callings and employments are being charged in the Union Territory of Jammu & Kashmir.", null, null, "Certificate of the Commissioner State Taxes, Jammu and Kashmir, 18 October 2021")]
    [InlineData("03", ProfessionalTaxLevyStatus.Levied, "The Punjab State Development Tax Act, 2018 (Punjab Act No. 11 of 2018)", "there shall be levied and collected a tax on professions, trades, callings and employments.", PunjabExtent, "but does not include any form of bonus or gratuity;", "Punjab State Development Tax Act, 2018, sections 1(2), 2(m) and 4(1)")]
    [InlineData("10", ProfessionalTaxLevyStatus.Levied, "The Bihar Tax on Professions, Trades, Callings and Employments Act, 2011 (Bihar Act 10, 2011)", "Schedule of rates under The Bihar Tax on Professions, Trades, Callings and Employments Act, 2011", null, null, "Schedule, as substituted by S.O. 28, Commercial Taxes Department, 2 February 2016")]
    [InlineData("13", ProfessionalTaxLevyStatus.Levied, "The Nagaland Professions, Trades, Callings and Employment Taxation Act, 1968 (Nagaland Act 4 of 1968)", "THE SCHEDULE (See Section 3)", null, null, "the Schedule, as published by the Department of Taxes, Nagaland")]
    [InlineData("15", ProfessionalTaxLevyStatus.Levied, "The Mizoram Professions, Trades, Callings and Employments Taxation Act, 1995 (Mizoram Act No. 7 of 1995)", "shall be liable to pay for each financial year a tax in respect of such profession, trade, calling or employment", MizoramExtent, "“total gross income” means aggregate of income derived from all sources.", "sections 1(2), 2(i) and 3, in the Taxation Department's copy of the Act as enacted")]
    [InlineData("16", ProfessionalTaxLevyStatus.Levied, "The Tripura Professions, Trades, Callings and Employments Taxation Act, 1997 (Tripura Act No. 3 of 1997)", "there shall be levied and collected a tax on professions, trades, callings and employments, in accordance with the provisions of this Act.", null, "as defined in section 17 of the Income-tax Act, 1961 (43 of 1961);", "Tripura Professions, Trades, Callings and Employments Taxation Act, 1997, sections 2(j) and 3(1)")]
    [InlineData("18", ProfessionalTaxLevyStatus.Levied, "The Assam Professions, Trades, Callings and Employments Taxation Act, 1947 (Assam Act VI of 1947)", "The tax under section 3 shall be payable by every person specified in that section", null, "as defined in section 17 of the Income Tax Act, 1961;", "long title and sections 2(g) and 4, as consolidated by the Commissionerate of Taxes")]
    [InlineData("19", ProfessionalTaxLevyStatus.Levied, "The West Bengal State Tax on Professions, Trades, Callings and Employments Act, 1979 (West Bengal Act VI of 1979)", "there shall be levied and collected a tax on professions, trades, callings and employments, in accordance with the provisions of this Act.", null, "the expression “on regular basis” shall mean for a period exceeding 180 days in a year.", "sections 2(j) and 3(1), as consolidated by the Directorate of Commercial Taxes")]
    [InlineData("21", ProfessionalTaxLevyStatus.NotLevied, "The Odisha State Tax on Professions, Trades, Callings and Employment (Repeal) Ordinance, 2026 (Odisha Ordinance 02 of 2026)", "This Ordinance shall come into force with effect from dated 1st April, 2026. [Section 2(1)] The Odisha State Tax on Professions, Trades, Callings and Employments Act, 2000 is hereby repealed.", null, null, "(Repeal) Ordinance, 2026, sections 1(2) and 2(1)")]
    [InlineData("24", ProfessionalTaxLevyStatus.Levied, "The Gujarat State Tax on Professions, Trades, Callings and Employments Act, 1976 (President's Act No. 11 of 1976)", "by the Designated Authorities for the benefit of the Panchayats, Municipalities, Municipal Corporations or, as the case may be, the State", null, null, "as amended by Gujarat Act No. 10 of 2008, section 4")]
    [InlineData("27", ProfessionalTaxLevyStatus.Levied, "The Maharashtra State Tax on Professions, Trades, Callings and Employments Act, 1975 (Mah. XVI of 1975)", "there shall be levied and collected a tax on professions, trades, callings and employments for the benefit of the State.", null, "but does not include bonus in any form and on any account or gratuity;", "sections 2(h) and 3(1), text as on 16 July 2025")]
    [InlineData("29", ProfessionalTaxLevyStatus.Levied, "The Karnataka Tax on Professions, Trades, Callings and Employments Act, 1976 (Karnataka Act 35 of 1976)", "There shall be levied and collected a tax on professions, trades, callings and employment for the benefit of the State.", null, "including any amount received by way of arrears of salary or bonus by whatever name called", "sections 2(j) and 3(1), as consolidated by the Commercial Taxes Department")]
    [InlineData("33", ProfessionalTaxLevyStatus.LocalBodies, "The Tamil Nadu Urban Local Bodies Act, 1998, Chapter VI-A", "There shall be levied by the council a tax on profession, trade, calling and employment. [Section 117-I(1)] Notwithstanding the repeal of the Tamil Nadu Tax on Professions, Trades, Callings and Employments Act, 1992", null, null, "sections 117-B(1) and 117-I(1)")]
    [InlineData("36", ProfessionalTaxLevyStatus.Levied, "The Telangana Tax on Professions, Trades, Callings and Employments Act, 1987", "There shall be levied and collected a tax on professions, trades, callings and employments for the benefit of the State.", null, null, "Telangana Tax on Professions, Trades, Callings and Employments Act, 1987, section 4(1)")]
    public async Task Levy_OfEachStateOnTheDayTheWindowOpens_IsWhatItsInstrumentSays(string stateCode, ProfessionalTaxLevyStatus status, string law, string words, string? extent, string? salary, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var levies = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<ProfessionalTaxLevies>(WindowOpens, TestContext.Current.CancellationToken))!.States;

        var found = Assert.Single(levies, candidate => candidate.StateCode == stateCode);
        Assert.Equal((status, law, extent), (found.Status, found.Law, found.Extent));
        Assert.Contains(words, found.Words, StringComparison.Ordinal);
        if (salary is null)
        {
            Assert.Null(found.Salary);
        }
        else
        {
            Assert.Contains(salary, found.Salary, StringComparison.Ordinal);
        }

        Assert.Contains(instrument, await SourceAsync(scope, LeviesKey, WindowOpens, stateCode), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Levies_OnTheDayTheWindowOpens_AreTheStatesWhoseInstrumentsWereReadAndNoOther()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var levies = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<ProfessionalTaxLevies>(WindowOpens, TestContext.Current.CancellationToken))!.States;

        Assert.Equal(["01", "03", "10", "13", "15", "16", "18", "19", "21", "24", "27", "29", "33", "36"], levies.Select(levy => levy.StateCode).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task PunjabLevy_OnTheDayTheWindowOpens_DefinesSalaryInTheActsOwnWordsCitingTheIncomeTaxAct1961()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var levies = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<ProfessionalTaxLevies>(WindowOpens, TestContext.Current.CancellationToken))!.States;

        Assert.Equal(
            "“salary” or “wage” includes pay or wages, dearness allowance and all other remunerations received by any person on regular basis, whether payable in cash or kind, and also includes perquisites and profits in lieu of salary as defined in section 17 of the Income Tax Act, 1961, but does not include any form of bonus or gratuity; Explanation.- Regular basis means a period exceeding one hundred and eighty days.",
            Assert.Single(levies, levy => levy.StateCode == "03").Salary);
    }

    [Theory]
    [InlineData("03-2(j).proviso", "03", null, null, null, null, "Provided that any person who earns wages on a casual basis", "Punjab State Development Tax Act, 2018, section 2(j), proviso")]
    [InlineData("03-4(2)", "03", null, null, null, null, "Senior citizens shall, however, be exempted as per the Income Tax Act, 1961.", "Punjab State Development Tax Act, 2018, section 4(2)")]
    [InlineData("03-4(3)", "03", null, null, null, null, "shall be payable under this Act only by those persons whose taxable income for the same financial year", "Punjab State Development Tax Act, 2018, section 4(3)")]
    [InlineData("15-5.proviso", "15", null, null, null, null, "shall not apply to a member of the Armed Forces of India", "Taxation Act, 1995, section 5, proviso")]
    [InlineData("16-2(f)", "16", null, null, null, null, "but does not include any person who earns wages on a casual basis;", "Taxation Act, 1997, section 2(f)")]
    [InlineData("16-27(1)", "16", null, null, null, null, "and are serving in any part of Tripura.", "Taxation Act, 1997, section 27(1)")]
    [InlineData("18-3.proviso(2)", "18", null, null, null, null, "shall not apply to a member of the Armed forces of India", "section 3, second proviso, with the Explanation inserted by Assam Act No. XXVIII of 2021")]
    [InlineData("18-FTX.76-2006-2", "18", 60, ProfessionalTaxAgeTest.Above, "500000", null, "any person above the age of 60 years … subject to the condition that his gross income does not exceed Rs.5.00 lakhs in a financial year", "Notification No. FTX.76/2006/2, Finance (Taxation) Department, Assam, 4 November 2006")]
    [InlineData("19-2(f)", "19", null, null, null, 180, "a person who earns wages on being employed for a period not exceeding 180 days in a year.", "section 2(f) and its Explanation")]
    [InlineData("19-26(1)", "19", null, null, null, null, "and are serving in any part of West Bengal.", "section 26(1)")]
    [InlineData("27-2(e)", "27", null, null, null, null, "but does not include any person who earns wages on a casual basis;", "section 2(e), text as on 16 July 2025")]
    [InlineData("27-27A(a)", "27", null, null, null, null, "(a) the members of the forces as defined in the Army Act, 1950 (46 of 1950)", "section 27A(a) and its Explanation, text as on 16 July 2025")]
    [InlineData("27-27A(b)", "27", null, null, null, null, "(b) the badli workers in the textile industry.", "section 27A(b), text as on 16 July 2025")]
    [InlineData("27-27A(c)(i)", "27", null, null, null, null, "(c) (i) any person with benchmark disability as defined in clause (r) of section 2 of the Rights of Persons with Disabilities Act, 2016;", "section 27A(c)(i) and its provisos, as substituted by Mah. Act No. XIX of 2023, section 2(1)")]
    [InlineData("27-27A(c)(ii)", "27", null, null, null, null, "(ii) parents or guardians of a child with a benchmark disability", "section 27A(c)(ii) and its provisos, as substituted by Mah. Act No. XIX of 2023, section 2(1)")]
    [InlineData("27-27A(d)", "27", null, null, null, null, "(d) women exclusively engaged as agents under the Mahila Pradhan Kshetriya Bachat Yojana of Directorate of Small Savings.", "section 27A(d), text as on 16 July 2025")]
    [InlineData("27-27A(f)", "27", 65, ProfessionalTaxAgeTest.Attained, null, null, "(f) the person who have completed the age of sixty-five years.", "section 27A(f), text as on 16 July 2025")]
    [InlineData("27-27A(h)", "27", null, null, null, null, "(vii) the Sashastra Seema Bal constituted under the Sashastra Seema Bal Act, 2007.", "section 27A(h), as substituted by Mah. Act No. XXXII of 2024, section 5, in force 1 September 2024")]
    [InlineData("29-2(h)", "29", null, null, null, null, "so engaged but does not include any person who earns wages on a casual basis;", "Employments Act, 1976, section 2(h)")]
    [InlineData("29-3(2).proviso(1)", "29", 60, ProfessionalTaxAgeTest.Attained, null, null, "no tax shall be payable by the individuals who are senior citizens having attained the age of sixty years", "section 3(2), first proviso")]
    [InlineData("29-3(2).proviso(3)", "29", null, null, null, 120, "does not exceed one hundred and twenty days in that year.", "section 3(2), third proviso")]
    [InlineData("29-FD-09-CPT-2023-3", "29", null, null, null, null, "who has total permanent disability of not less than 40% of both upper and lower extremity deformities subject to production of a certificate", KarnatakaNotification + ", item 3")]
    [InlineData("29-FD-09-CPT-2023-4", "29", null, null, null, null, "4. Individual person having single child and who has undergone sterilisation operation", KarnatakaNotification + ", item 4")]
    [InlineData("29-FD-09-CPT-2023-6", "29", null, null, null, null, "6. Person covered under Serial no. 1 of the above Schedule who are blind or deaf and dumb;", KarnatakaNotification + ", item 6")]
    [InlineData("29-FD-09-CPT-2023-7", "29", null, null, null, null, "subject to a maximum of two years from the date of their joining duty;", KarnatakaNotification + ", item 7")]
    [InlineData("29-FD-09-CPT-2023-10", "29", null, null, null, null, "10. Combatant and civilian non-combatant members of the Armed Forces who are governed by the Army Act, 1950, the Navy Act, 1957 and the Air Force Act, 1950;", KarnatakaNotification + ", item 10")]
    [InlineData("29-FD-09-CPT-2023-13", "29", null, null, null, null, "13. Central Para-Military Force (CPMF) Personnel; and", KarnatakaNotification + ", item 13")]
    public async Task Exemption_OnTheDayTheWindowOpens_IsTheClassAndConditionItsInstrumentSets(string exemption, string stateCode, int? age, ProfessionalTaxAgeTest? ageTest, string? incomeUpTo, int? daysUpTo, string words, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var exemptions = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<ProfessionalTaxExemptions>(WindowOpens, TestContext.Current.CancellationToken))!.Exemptions;

        var found = Assert.Single(exemptions, candidate => candidate.Exemption == exemption);
        Assert.Equal((stateCode, age, ageTest, RupeesOf(incomeUpTo), daysUpTo), (found.StateCode, found.Age, found.AgeTest, found.IncomeUpTo, found.DaysUpTo));
        Assert.Contains(words, found.Words, StringComparison.Ordinal);
        Assert.Contains(instrument, await SourceAsync(scope, ExemptionsKey, WindowOpens, exemption), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exemptions_OnTheDayTheWindowOpens_AreTheProvisionsOfTheInstrumentsReadAndNoOther()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var exemptions = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<ProfessionalTaxExemptions>(WindowOpens, TestContext.Current.CancellationToken))!.Exemptions;

        Assert.Equal(
            ["03-2(j).proviso", "03-4(2)", "03-4(3)", "15-5.proviso", "16-2(f)", "16-27(1)", "18-3.proviso(2)", "18-FTX.76-2006-2", "19-2(f)", "19-26(1)", "27-2(e)", "27-27A(a)", "27-27A(b)", "27-27A(c)(i)", "27-27A(c)(ii)", "27-27A(d)", "27-27A(f)", "27-27A(h)", "29-2(h)", "29-3(2).proviso(1)", "29-3(2).proviso(3)", "29-FD-09-CPT-2023-10", "29-FD-09-CPT-2023-13", "29-FD-09-CPT-2023-3", "29-FD-09-CPT-2023-4", "29-FD-09-CPT-2023-6", "29-FD-09-CPT-2023-7"],
            exemptions.Select(candidate => candidate.Exemption).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(LeviesKey)]
    [InlineData(SlabsKey)]
    [InlineData(ExemptionsKey)]
    public async Task Set_OfProfessionalTax_IsTheLawAnsweredForTheFirstDayOfATaxPeriod(string key)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, WindowOpens, TestContext.Current.CancellationToken);

        Assert.NotNull(set);
        Assert.Equal((ParameterSetBasis.Law, ParameterAsOf.TaxPeriodStart), (set.Basis, set.AsOf));
    }

    [Theory]
    [InlineData(LeviesKey, "2026-04-01")]
    [InlineData(SlabsKey, "2026-04-01,2026-10-01")]
    [InlineData(ExemptionsKey, "2026-04-01")]
    public async Task Versions_OfEachSet_AreTheDaysItsInstrumentsChangeIt(string key, string dates)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, WindowOpens, TestContext.Current.CancellationToken);

        Assert.Equal(dates.Split(',').Select(day => DateOnly.Parse(day, CultureInfo.InvariantCulture)).Reverse(), set!.Versions.Select(version => version.From));
    }

    [Theory]
    [InlineData(LeviesKey, "2026-04-01", 14, 0, 0, "Certificate of the Commissioner State Taxes, Jammu and Kashmir, 18 October 2021|Punjab State Development Tax Act, 2018 (Punjab Act No. 11 of 2018)|Bihar Tax on Professions, Trades, Callings and Employments Act, 2011 (Bihar Act 10, 2011)|Nagaland Professions, Trades, Callings and Employment Taxation Act, 1968 (Nagaland Act 4 of 1968)|Mizoram Professions, Trades, Callings and Employments Taxation Act, 1995 (Mizoram Act No. 7 of 1995)|Tripura Professions, Trades, Callings and Employments Taxation Act, 1997 (Tripura Act No. 3 of 1997)|Assam Professions, Trades, Callings and Employments Taxation Act, 1947 (Assam Act VI of 1947)|West Bengal State Tax on Professions, Trades, Callings and Employments Act, 1979 (West Bengal Act VI of 1979)|Odisha State Tax on Professions, Trades, Callings and Employment (Repeal) Ordinance, 2026 (Odisha Ordinance 02 of 2026)|Gujarat State Tax on Professions, Trades, Callings and Employments Act, 1976 (President's Act No. 11 of 1976)|Maharashtra State Tax on Professions, Trades, Callings and Employments Act, 1975 (Mah. XVI of 1975)|Karnataka Tax on Professions, Trades, Callings and Employments Act, 1976 (Karnataka Act 35 of 1976)|Tamil Nadu Urban Local Bodies Act, 1998, sections 117-B(1) and 117-I(1)|Telangana Tax on Professions, Trades, Callings and Employments Act, 1987, section 4(1)")]
    [InlineData(SlabsKey, "2026-04-01", 42, 0, 0, "Punjab State Development Tax Act, 2018 (Punjab Act No. 11 of 2018)|S.O. 28, Commercial Taxes Department, Bihar, 2 February 2016|Nagaland Professions, Trades, Callings and Employment Taxation Act, 1968 (Nagaland Act 4 of 1968)|Notification No. J.17011/1/2017-TAX, Taxation Department, Mizoram, 11 July 2024|Notification No. F.II-1(7)-TAX/99(P-I), Finance Department (Taxes & Excise), Tripura, 25 July 2018|Corrigendum No. F.II-1(7)-TAX/99(P-I), Finance Department (Taxes & Excise), Tripura, 30 October 2018|Notification eCF No. 169350/124, Finance (Taxation) Department, Assam, 1 April 2025|West Bengal State Tax on Professions, Trades, Callings and Employments Act, 1979 (West Bengal Act VI of 1979)|West Bengal Finance Act, 2016 (West Bengal Act VI of 2016)|Notification No. GHN-35-PFT-2022-S.3(2)(10)-Th, Finance Department, Gujarat, 8 April 2022|Maharashtra State Tax on Professions, Trades, Callings and Employments (Amendment) Act, 2023 (Mah. Act No. XIX of 2023)|Karnataka Tax on Profession, Trades, Callings and Employments (Amendment) Act, 2023 (Karnataka Act No. 14 of 2023)|Karnataka Tax on Profession, Trades, Callings and Employments (Amendment) Act, 2025 (Karnataka Act No. 33 of 2025)|Telangana Tax on Professions, Trades, Callings and Employments Act, 1987, First Schedule, entry 1")]
    [InlineData(SlabsKey, "2026-10-01", 0, 0, 5, "West Bengal Finance Act, 2026 (West Bengal Act XIII of 2026), Notification No. 756-L, 7 August 2026|Notification No. 1406-F.T., Finance Department (Revenue), West Bengal, 18 August 2026")]
    [InlineData(ExemptionsKey, "2026-04-01", 27, 0, 0, "Punjab State Development Tax Act, 2018 (Punjab Act No. 11 of 2018)|Mizoram Professions, Trades, Callings and Employments Taxation Act, 1995 (Mizoram Act No. 7 of 1995), section 5, proviso|Tripura Professions, Trades, Callings and Employments Taxation Act, 1997 (Tripura Act No. 3 of 1997)|Assam Professions, Trades, Callings and Employments Taxation Act, 1947 (Assam Act VI of 1947), section 3, second proviso|Notification No. FTX.76/2006/2, Finance (Taxation) Department, Assam, 4 November 2006|West Bengal State Tax on Professions, Trades, Callings and Employments Act, 1979 (West Bengal Act VI of 1979), sections 2(f) and 26(1)|Maharashtra State Tax on Professions, Trades, Callings and Employments Act, 1975 (Mah. XVI of 1975), sections 2(e) and 27A|Maharashtra State Tax on Professions, Trades, Callings and Employments (Amendment) Act, 2023 (Mah. Act No. XIX of 2023)|Maharashtra Tax Laws (Amendment) Act, 2024 (Mah. Act No. XXXII of 2024)|Karnataka Tax on Professions, Trades, Callings and Employments Act, 1976 (Karnataka Act 35 of 1976)|Notification-2, No. FD 09 CPT 2023, Finance Department, Karnataka, 13 July 2023")]
    public async Task Version_OfEachSet_CitesItsInstrumentsInOrderAndChangesTheRowsTheyChange(string key, string from, int added, int changed, int removed, string instruments)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, WindowOpens, TestContext.Current.CancellationToken);

        var version = Assert.Single(set!.Versions, candidate => candidate.From == DateOnly.Parse(from, CultureInfo.InvariantCulture));
        var cited = instruments.Split('|');
        Assert.Equal(cited.Length, version.Instruments.Count);
        Assert.All(cited.Zip(version.Instruments), pair => Assert.StartsWith(pair.First, pair.Second, StringComparison.Ordinal));
        Assert.Equal((added, changed, removed), (version.AddedCount, version.ChangedCount, version.RemovedCount));
    }

    [Theory]
    [InlineData(LeviesKey, "2026-03-31", null, null, 0)]
    [InlineData(LeviesKey, "2026-04-01", "2026-04-01", null, 14)]
    [InlineData(SlabsKey, "2026-03-31", null, null, 0)]
    [InlineData(SlabsKey, "2026-04-01", "2026-04-01", "2026-09-30", 42)]
    [InlineData(SlabsKey, "2026-09-30", "2026-04-01", "2026-09-30", 42)]
    [InlineData(SlabsKey, "2026-10-01", "2026-10-01", null, 37)]
    [InlineData(ExemptionsKey, "2026-03-31", null, null, 0)]
    [InlineData(ExemptionsKey, "2026-04-01", "2026-04-01", null, 27)]
    public async Task Version_OnTheFirstDayOfEachVersionAndTheDayBefore_IsTheVersionInForceWithItsRows(string key, string day, string? from, string? until, int rows)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, DateOnly.Parse(day, CultureInfo.InvariantCulture), TestContext.Current.CancellationToken);

        if (from is null)
        {
            Assert.Null(view);
            return;
        }

        Assert.NotNull(view);
        Assert.Equal((Day(from), Day(until), rows), ((DateOnly?)view.From, view.Until, view.Rows.Count));
    }

    [Fact]
    public async Task Sets_OnTheDayBeforeTheWindowOpens_HoldNothing()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();
        var day = WindowOpens.AddDays(-1);

        Assert.Null(await store.GetAsOfAsync<ProfessionalTaxLevies>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<ProfessionalTaxSlabs>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<ProfessionalTaxExemptions>(day, TestContext.Current.CancellationToken));
    }

    private static Money? RupeesOf(string? rupees) => rupees is null ? null : new Money(decimal.Parse(rupees, CultureInfo.InvariantCulture), Currency.Inr);

    private static DateOnly? Day(string? value) => value is null ? null : DateOnly.Parse(value, CultureInfo.InvariantCulture);

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string key, DateOnly day, string rowKey)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, day, TestContext.Current.CancellationToken);

        return Assert.Single(view!.Rows, row => row.Key == rowKey).Source;
    }
}
