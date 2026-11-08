using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Limits;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.IncomeTax.Tds;

// Golden values of the threshold limits, the cases with no deduction, the declarations for no deduction and the defined terms of the
// TDS provisions of the Income-tax Act, 2025, each pinned beside its instrument so that a wrong edit of a seed file fails a test
// (ADR-0063). Each case is a row on the day the Act came into force, or on the day the Finance Act, 2026 adds the declaration to a
// depository: its amount in rupees, its test, its age or count, null where the Act states none, and the provision its source cites.
[Collection(StatutoryRows.Name)]
public sealed class TdsLimitGoldenTests(ErpApiFactory factory)
{
    private static readonly DateOnly ActInForce = new(2026, 4, 1);

    private static readonly DateOnly DepositoryDeclarationsBegin = new(2027, 4, 1);

    [Theory]
    [InlineData("392(7)", "392(7)", "50000", TdsLimitTest.EqualToOrMore, TdsLimitMeasure.AggregatePayment, "Income-tax Act, 2025, section 392(7)")]
    [InlineData("393(1)-1(i)", "393(1)-1(i)", "20000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 1(i)]")]
    [InlineData("393(1)-1(ii)", "393(1)-1(ii)", "20000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 1(ii)]")]
    [InlineData("393(1)-2(i)", "393(1)-2(i)", "50000", TdsLimitTest.Exceeds, TdsLimitMeasure.MonthOrPart, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 2(i)]")]
    [InlineData("393(1)-2(ii)", "393(1)-2(ii)", "50000", TdsLimitTest.Exceeds, TdsLimitMeasure.MonthOrPart, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 2(ii)]")]
    [InlineData("393(1)-3(i)", "393(1)-3(i)", "5000000", TdsLimitTest.EqualToOrMore, TdsLimitMeasure.AllTransferees, "Finance Act, 2026, section 84(a)")]
    [InlineData("393(1)-3(ii)", "393(1)-3(ii)", "0", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 3(ii)]")]
    [InlineData("393(1)-3(iii)", "393(1)-3(iii)", "500000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 3(iii)]")]
    [InlineData("393(1)-4(i)", "393(1)-4(i)", "10000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(i)]")]
    [InlineData("393(1)-4(ii)", "393(1)-4(ii)", "0", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(ii)]")]
    [InlineData("393(1)-4(iii)", "393(1)-4(iii)", "0", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(iii)]")]
    [InlineData("393(1)-4(iv)", "393(1)-4(iv)", "0", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(iv)]")]
    [InlineData("393(1)-5(i)", "393(1)-5(i)", "10000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(i)]")]
    [InlineData("393(1)-5(ii).limit(a)", "393(1)-5(ii)", "100000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(ii)], threshold limit (a)")]
    [InlineData("393(1)-5(ii).limit(b)", "393(1)-5(ii)", "50000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(ii)], threshold limit (b)")]
    [InlineData("393(1)-5(iii)", "393(1)-5(iii)", "10000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(iii)]")]
    [InlineData("393(1)-6(i).limit(a)", "393(1)-6(i)", "30000", TdsLimitTest.Exceeds, TdsLimitMeasure.SingleSum, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(i)], threshold limit (a)")]
    [InlineData("393(1)-6(i).limit(b)", "393(1)-6(i)", "100000", TdsLimitTest.Exceeds, TdsLimitMeasure.AggregateOfSums, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(i)], threshold limit (b)")]
    [InlineData("393(1)-6(ii)", "393(1)-6(ii)", "5000000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(ii)]")]
    [InlineData("393(1)-6(iii).limit(i)", "393(1)-6(iii)", "50000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(iii)], threshold limit (i)")]
    [InlineData("393(1)-6(iii).limit(ii)", "393(1)-6(iii)", "0", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(iii)], threshold limit (ii)")]
    [InlineData("393(1)-7", "393(1)-7", "0", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) (Table: Sl. No. 7)")]
    [InlineData("393(1)-8(i)", "393(1)-8(i)", "100000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(i)]")]
    [InlineData("393(1)-8(ii)", "393(1)-8(ii)", "5000000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(ii)] and Note 1 below serial number 8")]
    [InlineData("393(1)-8(iii)", "393(1)-8(iii)", null, TdsLimitTest.NoFigure, null, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(iii)]")]
    [InlineData("393(1)-8(iv)", "393(1)-8(iv)", "20000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(iv)]")]
    [InlineData("393(1)-8(v)", "393(1)-8(v)", "0", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(v)]")]
    [InlineData("393(1)-8(vi)", "393(1)-8(vi)", "0", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(vi)]")]
    [InlineData("393(3)-1", "393(3)-1", "10000", TdsLimitTest.Exceeds, TdsLimitMeasure.SingleTransaction, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 1)")]
    [InlineData("393(3)-2", "393(3)-2", null, TdsLimitTest.NoFigure, null, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 2) and Note 1 of the Table")]
    [InlineData("393(3)-3", "393(3)-3", "10000", TdsLimitTest.Exceeds, TdsLimitMeasure.SingleTransaction, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 3)")]
    [InlineData("393(3)-4", "393(3)-4", "20000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 4)")]
    [InlineData("393(3)-5.limit(a)", "393(3)-5", "30000000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 5), threshold limit for a co-operative society")]
    [InlineData("393(3)-5.limit(b)", "393(3)-5", "10000000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 5), threshold limit (b)")]
    [InlineData("393(3)-6", "393(3)-6", "2500", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 6)")]
    [InlineData("393(3)-7", "393(3)-7", "20000", TdsLimitTest.Exceeds, TdsLimitMeasure.AmountOrAggregate, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 7)")]
    public async Task ThresholdLimit_OnTheDayTheActCameIntoForce_IsAsTheActPrintsIt(string limit, string entry, string? rupees, TdsLimitTest test, TdsLimitMeasure? measure, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var limits = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<TdsThresholdLimits>(ActInForce, TestContext.Current.CancellationToken))!.Limits;

        var found = Assert.Single(limits, candidate => candidate.Limit == limit);
        Assert.Equal((entry, RupeesOf(rupees), test, measure), (found.Entry, found.Amount, found.Test, found.Measure));
        Assert.Contains(instrument, await SourceAsync(scope, TdsThresholdLimits.Key, ActInForce, limit), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("393(4)-1", "Commission or Brokerage referred to in section 393(1) [Table: Sl. No. 1(ii)].", "393(1)-1(ii)", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 1]")]
    [InlineData("393(4)-2", "Rent referred to in section 393(1) [Table: Sl. No. 2(ii)].", "393(1)-2(ii)", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 2]")]
    [InlineData("393(4)-3", "Compensation on acquisition of certain immovable property referred to in section 393(1) [Table: Sl. No. 3(iii)].", "393(1)-3(iii)", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 3]")]
    [InlineData("393(4)-4", "Income in respect of units referred to in section 393(1) [Table: Sl. No. 4(i)].", "393(1)-4(i)", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 4]")]
    [InlineData("393(4)-5", "Income from units of a business trust referred to in section 393(1) [Table: Sl. No. 4(ii)].", "393(1)-4(ii)", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 5]")]
    [InlineData("393(4)-6", "Interest on securities referred to in section 393(1) [Table: Sl. No. 5(i)].", "393(1)-5(i)", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 6]")]
    [InlineData("393(4)-7(a)", "Interest other than interest on securities referred to in section 393(1) [Table: Sl. No. 5(ii) and 5(iii)].", null, null, null, "Finance Act, 2026, section 84(b)(i)")]
    [InlineData("393(4)-7(b)", "Interest other than interest on securities referred to in section 393(1) [Table: Sl. No. 5(ii) and 5(iii)].", null, "500000000", null, "Corrigenda of 30 January 2026")]
    [InlineData("393(4)-7(c)", "Interest other than interest on securities referred to in section 393(1) [Table: Sl. No. 5(ii) and 5(iii)].", null, null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 7.C(c)] without item (iv)(B), item (iv) as substituted by the Finance Act, 2026, section 84(b)(ii)")]
    [InlineData("393(4)-7(c)(iv)(B)", "Interest other than interest on securities referred to in section 393(1) [Table: Sl. No. 5(ii) and 5(iii)].", null, "50000", null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 7.C(c)(iv)(B)], as substituted by the Finance Act, 2026, section 84(b)(ii)")]
    [InlineData("393(4)-8(a)", "Payments to contractors referred to in section 393(1) [Table: Sl. No. 6(i)].", "393(1)-6(i)", null, 10, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 8.C(a)]")]
    [InlineData("393(4)-8(b)", "Payments to contractors referred to in section 393(1) [Table: Sl. No. 6(i)].", "393(1)-6(i)", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 8.C(b)]")]
    [InlineData("393(4)-9", "Fees for professional or technical services referred to in section 393(1) [Table: Sl. No. 6(iii)].", "393(1)-6(iii)", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 9]")]
    [InlineData("393(4)-10", "Dividend referred to in section 393(1) (Table: Sl. No. 7).", "393(1)-7", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 10.C(a) to (e)]")]
    [InlineData("393(4)-10(f)", "Dividend referred to in section 393(1) (Table: Sl. No. 7).", "393(1)-7", "10000", null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 10.C(f)]")]
    [InlineData("393(4)-11", "Payment by e-commerce operator to e-commerce participant referred to in section 393(1) [Table: Sl. No. 8(v)].", "393(1)-8(v)", "500000", null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 11]")]
    [InlineData("393(4)-12(a)", "Payment on transfer of virtual digital asset referred to in section 393(1) [Table: Sl. No. 8(vi)].", "393(1)-8(vi)", "50000", null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 12.C(a)]")]
    [InlineData("393(4)-12(b)", "Payment on transfer of virtual digital asset referred to in section 393(1) [Table: Sl. No. 8(vi)].", "393(1)-8(vi)", "10000", null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 12.C(b)]")]
    [InlineData("393(4)-13", "Income from units of a business trust referred to in section 393(2) (Table: Sl. No. 6).", "393(2)-6", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 13]")]
    [InlineData("393(4)-14", "Income in respect of units of investment fund referred to in section 393(2) (Table: Sl. No. 8).", "393(2)-8", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 14]")]
    [InlineData("393(4)-15", "Income in respect of units of non-residents referred to in section 393(2) (Table: Sl. No. 10).", "393(2)-10", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 15]")]
    [InlineData("393(4)-16", "Income of Foreign Institutional Investors from securities referred to in section 393(2) (Table: Sl. No. 15).", "393(2)-15", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 16]")]
    [InlineData("393(4)-17", "Income of Specified Fund from securities referred to in section 393(2) (Table: Sl. No. 16).", "393(2)-16", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 17]")]
    [InlineData("393(4)-18", "Payment of certain amounts in cash referred to in section 393(3) (Table: Sl. No. 5).", "393(3)-5", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 18]")]
    [InlineData("393(4)-19", "Payment in respect of deposits under National Savings Scheme, etc., referred to in section 393(3) (Table: Sl. No. 6).", "393(3)-6", null, null, "Income-tax Act, 2025, section 393(4) [Table: Sl. No. 19]")]
    [InlineData("393(5)", "Irrespective of anything contained in this Chapter", null, null, null, "Income-tax Act, 2025, section 393(5)")]
    [InlineData("393(8)", "Irrespective of anything contained in sub-section (6)", null, null, null, "Income-tax Act, 2025, section 393(8)")]
    [InlineData("393(9)", "Irrespective of anything contained in this Chapter", null, null, null, "Income-tax Act, 2025, section 393(9)")]
    public async Task NoDeductionCase_OnTheDayTheActCameIntoForce_IsAsTheActPrintsIt(string key, string provisions, string? entry, string? rupees, int? count, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var cases = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<TdsNoDeductionCases>(ActInForce, TestContext.Current.CancellationToken))!.Cases;

        var found = Assert.Single(cases, candidate => candidate.Case == key);
        Assert.Equal((provisions, entry, RupeesOf(rupees), count), (found.Provisions, found.Entry, found.Amount, found.Count));
        Assert.Contains(instrument, await SourceAsync(scope, TdsNoDeductionCases.Key, ActInForce, key), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("393(4)-7(c)", "(iv) on the compensation amount awarded by a Motor Accidents Claims Tribunal— (A) to an individual; or (v) or payable by an infrastructure capital company;", "(B) to a person other than an individual")]
    [InlineData("393(4)-7(c)(iv)(B)", "(c) interest income credited or paid— (iv) on the compensation amount awarded by a Motor Accidents Claims Tribunal— (B) to a person other than an individual, where the aggregate interest on such compensation does not exceed ₹ 50000 during the tax year;", "(A) to an individual")]
    [InlineData("393(4)-8(a)", "(ii) that contractor owns ten or less goods carriages at any time during the tax year;", "(b) where such sum")]
    [InlineData("393(4)-8(b)", "(b) where such sum is credited or paid by individual or Hindu undivided family exclusively for personal purposes of such individual or any member of Hindu undivided family.", "goods carriages")]
    [InlineData("393(4)-10", "(e) any other person as may be notified by the Central Government in this behalf;", "(f) a shareholder, being an individual")]
    [InlineData("393(4)-10(f)", "Dividend income credited or paid to— (f) a shareholder, being an individual, if— (I) the dividend is paid by the company by any mode other than cash; and (II) amount or aggregate of amounts of such dividend distributed or paid or likely to be distributed or paid during the tax year does not exceed ₹10,000.", "(e) any other person")]
    public async Task NoDeductionCase_OfAClauseItsFigureBoundsAlone_IsACaseOfItsOwnAndLeavesTheOtherClausesOut(string key, string words, string leftOut)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var cases = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<TdsNoDeductionCases>(ActInForce, TestContext.Current.CancellationToken))!.Cases;

        var condition = Assert.Single(cases, candidate => candidate.Case == key).Condition;
        Assert.Contains(words, condition, StringComparison.Ordinal);
        Assert.DoesNotContain(leftOut, condition, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("393(6)-1", "2026-04-01", "An individual being a resident.", null, "Income-tax Act, 2025, section 393(6) [Table: Sl. No. 1]")]
    [InlineData("393(6)-2", "2026-04-01", "Any person not being a company or a firm or an individual covered in Sl. No. (1).", null, "Income-tax Act, 2025, section 393(6) [Table: Sl. No. 2]")]
    [InlineData("393(6)-Note", "2026-04-01", "a person referred to in column B of the Table, other than an individual being a resident who is of the age of sixty years or more at any time during the tax year", 60, "Income-tax Act, 2025, section 393(6), the Note below the Table")]
    [InlineData("393(6)-1", "2027-04-01", "An individual being a resident.", null, "Income-tax Act, 2025, section 393(6) [Table: Sl. No. 1]")]
    [InlineData("393(6)-2", "2027-04-01", "Any person not being a company or a firm or an individual covered in Sl. No. (1).", null, "Income-tax Act, 2025, section 393(6) [Table: Sl. No. 2]")]
    [InlineData("393(6)-Note", "2027-04-01", "a person referred to in column B of the Table, other than an individual being a resident who is of the age of sixty years or more at any time during the tax year", 60, "Income-tax Act, 2025, section 393(6), the Note below the Table")]
    [InlineData("393(6)(b)", "2027-04-01", "The declaration referred in clause (a) may also be furnished electronically to a depository, as defined in section (2)(e) of the Depositories Act, 1996", null, "Finance Act, 2026, section 84(c), with effect from 1 April 2027")]
    public async Task Declaration_OnTheDayItsVersionBegins_IsAsTheActPrintsIt(string row, string day, string person, int? age, string instrument)
    {
        var date = DateOnly.Parse(day, CultureInfo.InvariantCulture);
        await using var scope = factory.Services.CreateAsyncScope();

        var declarations = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<TdsDeclarations>(date, TestContext.Current.CancellationToken))!.Declarations;

        var found = Assert.Single(declarations, candidate => candidate.Row == row);
        Assert.Equal((person, age), (found.Person, found.Age));
        Assert.Contains(instrument, await SourceAsync(scope, TdsDeclarations.Key, date, row), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeclarationToADepository_BeforeAndFromTheDayTheFinanceAct2026InsertsIt_IsAbsentThenPresent()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        var before = (await store.GetAsOfAsync<TdsDeclarations>(DepositoryDeclarationsBegin.AddDays(-1), TestContext.Current.CancellationToken))!.Declarations;
        var onTheDay = (await store.GetAsOfAsync<TdsDeclarations>(DepositoryDeclarationsBegin, TestContext.Current.CancellationToken))!.Declarations;

        Assert.Equal(["393(6)-1", "393(6)-2", "393(6)-Note"], before.Select(declaration => declaration.Row).Order(StringComparer.Ordinal));
        Assert.Equal(["393(6)(b)", "393(6)-1", "393(6)-2", "393(6)-Note"], onTheDay.Select(declaration => declaration.Row).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("402(11)(l)-business", "10000000", null, TdsTermTest.Exceeds, "Income-tax Act, 2025, section 402(11)(l)")]
    [InlineData("402(11)(l)-profession", "5000000", null, TdsTermTest.Exceeds, "Income-tax Act, 2025, section 402(11)(l)")]
    [InlineData("402(37)(b)-business", "10000000", null, TdsTermTest.Exceeds, "Income-tax Act, 2025, section 402(37)(b)")]
    [InlineData("402(37)(b)-profession", "5000000", null, TdsTermTest.Exceeds, "Income-tax Act, 2025, section 402(37)(b)")]
    [InlineData("402(6)-1", "100000000", null, TdsTermTest.Exceeds, "Income-tax Act, 2025, section 402(6) [Table: Sl. No. 1]")]
    [InlineData("402(39)(a)", null, 75, TdsTermTest.EqualToOrMore, "Income-tax Act, 2025, section 402(39)")]
    [InlineData("2(100)", null, 60, TdsTermTest.EqualToOrMore, "Income-tax Act, 2025, section 2(100)")]
    [InlineData("402(47)(f)", null, null, null, "Finance Act, 2026, section 90(b)")]
    public async Task Term_OnTheDayTheActCameIntoForce_IsAsTheActDefinesIt(string term, string? rupees, int? age, TdsTermTest? test, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var terms = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<TdsTerms>(ActInForce, TestContext.Current.CancellationToken))!.Terms;

        var found = Assert.Single(terms, candidate => candidate.Term == term);
        Assert.Equal((RupeesOf(rupees), age, test), (found.Amount, found.Age, found.Test));
        Assert.Contains(instrument, await SourceAsync(scope, TdsTerms.Key, ActInForce, term), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Work_OnTheDayTheActCameIntoForce_IncludesTheSupplyOfManpowerTheFinanceAct2026Inserted()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var terms = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<TdsTerms>(ActInForce, TestContext.Current.CancellationToken))!.Terms;

        Assert.Equal("(47) “work” shall include— (f) supply of manpower to a person to work under his supervision, control or direction.", Assert.Single(terms, term => term.Term == "402(47)(f)").Words);
    }

    private static Money? RupeesOf(string? rupees) => rupees is null ? null : new Money(decimal.Parse(rupees, CultureInfo.InvariantCulture), Currency.Inr);

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string key, DateOnly day, string rowKey)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, day, TestContext.Current.CancellationToken);

        return Assert.Single(view!.Rows, row => row.Key == rowKey).Source;
    }
}
