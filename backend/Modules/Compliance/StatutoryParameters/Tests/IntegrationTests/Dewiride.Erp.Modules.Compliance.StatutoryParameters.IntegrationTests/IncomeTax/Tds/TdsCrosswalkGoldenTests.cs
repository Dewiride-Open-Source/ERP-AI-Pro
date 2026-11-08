using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Crosswalk;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.IncomeTax.Tds;

// Golden values of the ERP's reading of which provision of the Income-tax Act, 2025 governs a payment on which tax was deducted
// under the Income-tax Act, 1961, each pinned beside the provisions its source names so that a wrong edit of a seed file fails a
// test (ADR-0063). The pairs hold from the day the 2025 Act came into force with no end, and the 1961 headings are TRACES's.
[Collection(StatutoryRows.Name)]
public sealed class TdsCrosswalkGoldenTests(ErpApiFactory factory)
{
    private const string Crosswalk = "income-tax.tds-crosswalk";

    private const string FinanceActSections = "Finance Act, 2026 (4 of 2026), 30 March 2026, section 3(7) and (10) and sections 84, 86 and 90, in force 1 April 2026 (section 1(2)(a))";

    private const string TracesReviewed = "“Last reviewed and updated on : 31-March-2026”";

    private static readonly DateOnly ActInForce = new(2026, 4, 1);

    [Theory]
    [InlineData("192.392(1)", "192", "392(1)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.DepartmentStates, "Income-tax Act, 2025, section 392(1); Income-tax Act, 1961, section 192")]
    [InlineData("192A.392(7)", "192A", "392(7)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 392(7); Income-tax Act, 1961, section 192A")]
    [InlineData("194D.393(1)-1(i)", "194D", "393(1)-1(i)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 1(i)]; Income-tax Act, 1961, section 194D")]
    [InlineData("194H.393(1)-1(ii)", "194H", "393(1)-1(ii)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 1(ii)]; Income-tax Act, 1961, section 194H")]
    [InlineData("194-IB.393(1)-2(i)", "194-IB", "393(1)-2(i)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.DepartmentForms, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 2(i)]; Income-tax Act, 1961, section 194-IB")]
    [InlineData("194-I.393(1)-2(ii)", "194-I", "393(1)-2(ii)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 2(ii)]; Income-tax Act, 1961, section 194-I")]
    [InlineData("194-IA.393(1)-3(i)", "194-IA", "393(1)-3(i)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.DepartmentForms, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 3(i)]; Income-tax Act, 1961, section 194-IA")]
    [InlineData("194-IC.393(1)-3(ii)", "194-IC", "393(1)-3(ii)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 3(ii)]; Income-tax Act, 1961, section 194-IC")]
    [InlineData("194LA.393(1)-3(iii)", "194LA", "393(1)-3(iii)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 3(iii)]; Income-tax Act, 1961, section 194LA")]
    [InlineData("194K.393(1)-4(i)", "194K", "393(1)-4(i)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(i)]; Income-tax Act, 1961, section 194K")]
    [InlineData("194LBA.393(1)-4(ii)", "194LBA", "393(1)-4(ii)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(ii)]; Income-tax Act, 1961, section 194LBA")]
    [InlineData("194LBB.393(1)-4(iii)", "194LBB", "393(1)-4(iii)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(iii)]; Income-tax Act, 1961, section 194LBB")]
    [InlineData("194LBC.393(1)-4(iv)", "194LBC", "393(1)-4(iv)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 4(iv)]; Income-tax Act, 1961, section 194LBC")]
    [InlineData("193.393(1)-5(i)", "193", "393(1)-5(i)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(i)]; Income-tax Act, 1961, section 193")]
    [InlineData("194A.393(1)-5(ii)", "194A", "393(1)-5(ii)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(ii)]; Income-tax Act, 1961, section 194A")]
    [InlineData("194A.393(1)-5(iii)", "194A", "393(1)-5(iii)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 5(iii)]; Income-tax Act, 1961, section 194A")]
    [InlineData("194C.393(1)-6(i)", "194C", "393(1)-6(i)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.DepartmentStates, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(i)]; Income-tax Act, 1961, section 194C")]
    [InlineData("194M.393(1)-6(ii)", "194M", "393(1)-6(ii)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.DepartmentForms, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(ii)]; Income-tax Act, 1961, section 194M")]
    [InlineData("194J.393(1)-6(iii)", "194J", "393(1)-6(iii)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 6(iii)]; Income-tax Act, 1961, section 194J")]
    [InlineData("194.393(1)-7", "194", "393(1)-7", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) (Table: Sl. No. 7); Income-tax Act, 1961, section 194")]
    [InlineData("194DA.393(1)-8(i)", "194DA", "393(1)-8(i)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(i)]; Income-tax Act, 1961, section 194DA")]
    [InlineData("194Q.393(1)-8(ii)", "194Q", "393(1)-8(ii)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(ii)]; Income-tax Act, 1961, section 194Q")]
    [InlineData("194P.393(1)-8(iii)", "194P", "393(1)-8(iii)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(iii)]; Income-tax Act, 1961, section 194P")]
    [InlineData("194R.393(1)-8(iv)", "194R", "393(1)-8(iv)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(iv)]; Income-tax Act, 1961, section 194R")]
    [InlineData("194-O.393(1)-8(v)", "194-O", "393(1)-8(v)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(v)]; Income-tax Act, 1961, section 194-O")]
    [InlineData("194S.393(1)-8(vi)", "194S", "393(1)-8(vi)", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.DepartmentForms, "Income-tax Act, 2025, section 393(1) [Table: Sl. No. 8(vi)]; Income-tax Act, 1961, section 194S")]
    [InlineData("194E.393(2)-1", "194E", "393(2)-1", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 1); Income-tax Act, 1961, section 194E")]
    [InlineData("194LC.393(2)-2", "194LC", "393(2)-2", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 2); Income-tax Act, 1961, section 194LC")]
    [InlineData("194LC.393(2)-3", "194LC", "393(2)-3", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 3); Income-tax Act, 1961, section 194LC")]
    [InlineData("194LC.393(2)-4", "194LC", "393(2)-4", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 4); Income-tax Act, 1961, section 194LC")]
    [InlineData("194LB.393(2)-5", "194LB", "393(2)-5", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 5); Income-tax Act, 1961, section 194LB")]
    [InlineData("194LBA.393(2)-6", "194LBA", "393(2)-6", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 6); Income-tax Act, 1961, section 194LBA")]
    [InlineData("194LBA.393(2)-7", "194LBA", "393(2)-7", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 7); Income-tax Act, 1961, section 194LBA")]
    [InlineData("194LBB.393(2)-8", "194LBB", "393(2)-8", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 8); Income-tax Act, 1961, section 194LBB")]
    [InlineData("194LBC.393(2)-9", "194LBC", "393(2)-9", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 9); Income-tax Act, 1961, section 194LBC")]
    [InlineData("196A.393(2)-10", "196A", "393(2)-10", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 10); Income-tax Act, 1961, section 196A")]
    [InlineData("196B.393(2)-11", "196B", "393(2)-11", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 11); Income-tax Act, 1961, section 196B")]
    [InlineData("196B.393(2)-12", "196B", "393(2)-12", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 12); Income-tax Act, 1961, section 196B")]
    [InlineData("196C.393(2)-13", "196C", "393(2)-13", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 13); Income-tax Act, 1961, section 196C")]
    [InlineData("196C.393(2)-14", "196C", "393(2)-14", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 14); Income-tax Act, 1961, section 196C")]
    [InlineData("196D.393(2)-15", "196D", "393(2)-15", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 15); Income-tax Act, 1961, section 196D")]
    [InlineData("196D.393(2)-16", "196D", "393(2)-16", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 16); Income-tax Act, 1961, section 196D")]
    [InlineData("195.393(2)-17", "195", "393(2)-17", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(2) (Table: Sl. No. 17); Income-tax Act, 1961, section 195")]
    [InlineData("194B.393(3)-1", "194B", "393(3)-1", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 1); Income-tax Act, 1961, section 194B")]
    [InlineData("194BA.393(3)-2", "194BA", "393(3)-2", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 2); Income-tax Act, 1961, section 194BA")]
    [InlineData("194BB.393(3)-3", "194BB", "393(3)-3", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 3); Income-tax Act, 1961, section 194BB")]
    [InlineData("194G.393(3)-4", "194G", "393(3)-4", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 4); Income-tax Act, 1961, section 194G")]
    [InlineData("194N.393(3)-5", "194N", "393(3)-5", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 5); Income-tax Act, 1961, section 194N")]
    [InlineData("194EE.393(3)-6", "194EE", "393(3)-6", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 6); Income-tax Act, 1961, section 194EE")]
    [InlineData("194T.393(3)-7", "194T", "393(3)-7", TdsCrosswalkKind.Deduction, TdsCrosswalkTie.ActWords, "Income-tax Act, 2025, section 393(3) (Table: Sl. No. 7); Income-tax Act, 1961, section 194T")]
    [InlineData("194LD.none", "194LD", null, TdsCrosswalkKind.NoCounterpart, TdsCrosswalkTie.ActWords, "Income-tax Act, 1961, section 194LD")]
    [InlineData("197.395(1)", "197", "395(1)", TdsCrosswalkKind.Certificate, TdsCrosswalkTie.DepartmentStates, "Income-tax Act, 2025, section 395(1); Income-tax Act, 1961, section 197")]
    public async Task Pair_OnTheDayTheIncomeTaxAct2025CameIntoForce_TiesTheProvisionsItsSourceNames(
        string pair,
        string oldProvision,
        string? newProvision,
        TdsCrosswalkKind kind,
        TdsCrosswalkTie tie,
        string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var found = Assert.Single(await PairsAsync(scope, ActInForce), candidate => candidate.Pair == pair);

        Assert.Equal((oldProvision, newProvision, kind, tie), (found.OldProvision, found.NewProvision, found.Kind, found.Tie));
        Assert.Equal(kind == TdsCrosswalkKind.Deduction ? newProvision : null, found.NewEntry);
        Assert.StartsWith(instrument, await SourceAsync(scope, pair), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("192.392(1)", "Salary")]
    [InlineData("192A.392(7)", "Payment of accumulated balance due to an employee.(applicable from 01.06.2015)")]
    [InlineData("194D.393(1)-1(i)", "Insurance Commission")]
    [InlineData("194H.393(1)-1(ii)", "Commission / Brokerage")]
    [InlineData("194-IB.393(1)-2(i)", "Payment of rent by certain individuals or Hindu undivided family")]
    [InlineData("194-I.393(1)-2(ii)", "Rent")]
    [InlineData("194-IA.393(1)-3(i)", "Transfer of certain immovable property other than agriculture land (w.e.f. 1-6-2013)")]
    [InlineData("194-IC.393(1)-3(ii)", "Payment under Specified agreement")]
    [InlineData("194LA.393(1)-3(iii)", "Immovable Property")]
    [InlineData("194K.393(1)-4(i)", "Payment of Dividend by Mutual Funds (Applicable From 01-Apr-2020)")]
    [InlineData("194LBA.393(1)-4(ii)", "Certain income from units of a business trust to Residents")]
    [InlineData("194LBB.393(1)-4(iii)", "Income in respect of units of investment fund.")]
    [InlineData("194LBC.393(1)-4(iv)", "Income in respect of investment in securitisation trust.")]
    [InlineData("193.393(1)-5(i)", "Interest on securities")]
    [InlineData("194A.393(1)-5(ii)", "Banks / Co-operative society engaged in business of banking / Post Office")]
    [InlineData("194A.393(1)-5(iii)", "Interest other than interest on securities - in any Others Case")]
    [InlineData("194C.393(1)-6(i)", "Payment to Contractors")]
    [InlineData("194M.393(1)-6(ii)", "Payment of certain sums by certain individuals or Hindu undivided family")]
    [InlineData("194J.393(1)-6(iii)", "Fees for Professional/Technical Services")]
    [InlineData("194.393(1)-7", "Dividends")]
    [InlineData("194DA.393(1)-8(i)", "Payment in respect of life insurance policy (applicable from 01.10.2014)")]
    [InlineData("194Q.393(1)-8(ii)", "TDS on Purchase of Goods exceeding Rs. 50 Lakhs (Applicable From 01-July-2021)")]
    [InlineData("194P.393(1)-8(iii)", "TDS in case of Specified Senior Citizen")]
    [InlineData("194R.393(1)-8(iv)", "TDS in case any benefit or perquisite (arising from business or the exercise of a profession) is provided (Applicable From 01-July-2022)")]
    [InlineData("194-O.393(1)-8(v)", "TDS on e-commerce participants (Applicable From 01-Oct-2020)")]
    [InlineData("194S.393(1)-8(vi)", "TDS on payment on transfer of Virtual Digital Asset ( Applicable From 01-July-2022)")]
    [InlineData("194E.393(2)-1", "Payment to Non-Resident Sportsmen or Sports Association")]
    [InlineData("194LC.393(2)-2", "Income by way of interest by an Indian specified company to a non-resident / foreign company on foreign currency approved loan /long-term infrastructure bonds from outside India (applicable from July 1, 2012)")]
    [InlineData("194LC.393(2)-3", "Income by way of interest by an Indian specified company to a non-resident / foreign company on foreign currency approved loan /long-term infrastructure bonds from outside India (applicable from July 1, 2012)")]
    [InlineData("194LC.393(2)-4", "Income by way of interest by an Indian specified company to a non-resident / foreign company on foreign currency approved loan /long-term infrastructure bonds from outside India (applicable from July 1, 2012)")]
    [InlineData("194LB.393(2)-5", "Income by way of interest from infrastructure debt fund (non-resident)")]
    [InlineData("194LBA.393(2)-6", "Certain income from units of a business trust to Non Resident :-")]
    [InlineData("194LBA.393(2)-7", "Payment of the nature referred to in section 10(23FCA) by business trust to unit holders")]
    [InlineData("194LBB.393(2)-8", "Income in respect of units of investment fund.")]
    [InlineData("194LBC.393(2)-9", "Income in respect of investment in securitisation trust.")]
    [InlineData("196A.393(2)-10", "Foreign comp unit holder of MF")]
    [InlineData("196B.393(2)-11", "Income from units")]
    [InlineData("196B.393(2)-12", "Income from units")]
    [InlineData("196C.393(2)-13", "Income from foreign currency bonds or GDR (including long-term capital gains on transfer of such bonds) (not being dividend)")]
    [InlineData("196C.393(2)-14", "Income from foreign currency bonds or GDR (including long-term capital gains on transfer of such bonds) (not being dividend)")]
    [InlineData("196D.393(2)-15", "Income of foreign institutional investors from securities under sub-section (1)")]
    [InlineData("196D.393(2)-16", "Income of specified fund from securities referred to in clause (a) of sub-section (1) of section 115AD (other than interest income referred to in section 194LD)")]
    [InlineData("195.393(2)-17", "Other Sums")]
    [InlineData("194B.393(3)-1", "Winning from Lotteries or crossword puzzle, etc.")]
    [InlineData("194BA.393(3)-2", "Winnings from online games (Applicable From 01-April-2023)")]
    [InlineData("194BB.393(3)-3", "Winnings from Horse Race")]
    [InlineData("194G.393(3)-4", "Commission - Lottery")]
    [InlineData("194N.393(3)-5", "Payment of certain amounts in cash.")]
    [InlineData("194EE.393(3)-6", "Payments out of deposits under NSS")]
    [InlineData("194T.393(3)-7", "Payment of salary, remuneration, commission, bonus or interest to a partner of firm")]
    [InlineData("194LD.none", "Interest on certain bonds and govt. Securities (from 01-06-2013)")]
    [InlineData("197.395(1)", null)]
    public async Task Heading_OfEachPair_IsTheTextTracesShowsForThe1961Provision(string pair, string? heading)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        Assert.Equal(heading, Assert.Single(await PairsAsync(scope, ActInForce), candidate => candidate.Pair == pair).Heading1961);
    }

    [Fact]
    public async Task Pairs_OnEveryDayOfTheIncomeTaxAct2025_AreTheFiftyTwoWhoseTwoSidesWereRead()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        foreach (var day in new[] { ActInForce, new DateOnly(2027, 4, 1) })
        {
            var pairs = await PairsAsync(scope, day);

            Assert.Equal(52, pairs.Count);
            Assert.DoesNotContain(pairs, candidate => candidate.OldProvision is "195(6)" or "197A" or "206AA" || candidate.Pair == "194N.none");
        }

        Assert.Null(await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<TdsCrosswalk>(ActInForce.AddDays(-1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Version_OfTheCrosswalk_CitesTheFinanceAct2026BySection3AndTheTracesPagesAsReviewed()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var history = scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>();

        var set = await history.GetSetAsync(Crosswalk, ActInForce, TestContext.Current.CancellationToken);
        var view = await history.GetVersionAsync(Crosswalk, ActInForce, TestContext.Current.CancellationToken);

        Assert.Equal([ActInForce], set!.Versions.Select(version => version.From));
        Assert.Equal((52, 0, 0), (view!.Added.Count, view.Changed.Count, view.Removed.Count));
        Assert.Contains(FinanceActSections, view.Instruments);
        Assert.DoesNotContain(view.Instruments, cited => cited.StartsWith("Finance Act, 2026", StringComparison.Ordinal) && cited.Contains("section 2(", StringComparison.Ordinal));
        Assert.Contains(view.Instruments, cited => cited.Contains("https://traces61contents.tdscpc.gov.in/en/tds-2025-2026.html", StringComparison.Ordinal) && cited.Contains(TracesReviewed, StringComparison.Ordinal));
        Assert.Contains(view.Instruments, cited => cited.Contains("https://traces61contents.tdscpc.gov.in/en/section-codes-26A.html", StringComparison.Ordinal) && cited.Contains(TracesReviewed, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reasons_OfThePairs_CiteTheFinanceAct2026BySection3()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var pairs = await PairsAsync(scope, ActInForce);

        Assert.Contains("section 3(7) of the Finance Act, 2026 (Table, Sl. No. 1)", Assert.Single(pairs, candidate => candidate.Pair == "194D.393(1)-1(i)").Why, StringComparison.Ordinal);
        Assert.Contains("section 3(10)(ii) of the Finance Act, 2026", Assert.Single(pairs, candidate => candidate.Pair == "192.392(1)").Why, StringComparison.Ordinal);
        Assert.DoesNotContain(pairs, candidate => candidate.Why.Contains("section 2(7) of the Finance Act, 2026", StringComparison.Ordinal) || candidate.Why.Contains("section 2(10)", StringComparison.Ordinal));
    }

    private static async Task<IReadOnlyList<TdsCrosswalkPair>> PairsAsync(AsyncServiceScope scope, DateOnly day) =>
        (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<TdsCrosswalk>(day, TestContext.Current.CancellationToken))?.Pairs
        ?? throw new InvalidOperationException($"{Crosswalk} holds nothing for {day:yyyy-MM-dd}.");

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string pair)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(Crosswalk, ActInForce, TestContext.Current.CancellationToken);

        return Assert.Single(view!.Rows, row => row.Key == pair).Source;
    }
}
