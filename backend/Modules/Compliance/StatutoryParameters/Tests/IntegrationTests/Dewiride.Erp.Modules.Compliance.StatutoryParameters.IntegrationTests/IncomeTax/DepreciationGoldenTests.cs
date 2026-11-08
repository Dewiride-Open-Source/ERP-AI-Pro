using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Depreciation;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.IncomeTax;

// Golden values of income-tax depreciation for tax years from 2026-27: every rate of the Table in Appendix I to the Income-tax
// Rules, 2026, the nine Notes below it, and the half rate, additional depreciation, caps and laboratory-technology rate of
// section 33 of the Income-tax Act, 2025 and rule 25, each pinned beside its instrument so that a wrong edit of a seed file fails
// a test (ADR-0063).
[Collection(StatutoryRows.Name)]
public sealed class DepreciationGoldenTests(ErpApiFactory factory)
{
    private const string Rates = "income-tax.depreciation-rates";

    private const string Notes = "income-tax.depreciation-notes";

    private const string Allowances = "income-tax.depreciation-allowances";

    private const string RulesForTheTable = "Income-tax Rules, 2026, G.S.R. 198(E), 20 March 2026 (Notification No. 22/2026, F. No. 370142/41/2025-TPL), rule 25(1) and Appendix I, in force 1 April 2026 (rule 1(2))";

    private const string ActForTheTable = "Income-tax Act, 2025 (30 of 2025), section 33(3)(a), in force 1 April 2026 (section 1(3))";

    private const string RulesForTheNotes = "Income-tax Rules, 2026, G.S.R. 198(E), 20 March 2026 (Notification No. 22/2026, F. No. 370142/41/2025-TPL), Appendix I, Notes, in force 1 April 2026 (rule 1(2))";

    private const string ActForTheAllowances = "Income-tax Act, 2025 (30 of 2025), section 33(4), (8) and (9), in force 1 April 2026 (section 1(3))";

    private const string RulesForTheAllowances = "Income-tax Rules, 2026, G.S.R. 198(E), 20 March 2026 (Notification No. 22/2026, F. No. 370142/41/2025-TPL), rule 25(2) and (7), in force 1 April 2026 (rule 1(2))";

    private const string CapOf40Percent = "shall not exceed 40% of the written down value of such block of assets";

    private const string Note1 = "\"Buildings\" include roads, bridges, culverts, wells and tubewells.";

    private const string Note2 = "A building shall be deemed to be a building used mainly for residential purposes, if the built up floor area thereof used for residential purposes is not less than sixty-six and two-third per cent of its total built-up floor area and shall include any such building in the factory premises.";

    private const string Note3 = "In respect of any structure or work by way of renovation or improvement in or in relation to a building referred to in section 33(6), the percentage to be applied will be the percentage specified against sub-item (1) or (2) of item 1 of PART A as may be appropriate to the class of building in or in relation to which the renovation or improvement is effected. Where the structure is constructed or the work is done by way of extension of any such building, the percentage to be applied would be such percentage as would be appropriate, as if the structure or work constituted a separate building.";

    private const string Note4 = "Water treatment system includes system for desalination, demineralisation and purification of water.";

    private const string Note5 = "\"Electrical fittings\" include electrical wiring, switches, sockets, other fittings and fans, etc.";

    private const string Note6 = "\"Commercial vehicle\" means \"heavy goods vehicle\", \"heavy passenger motor vehicle\", \"light motor vehicle\", \"medium goods vehicle\" and \"medium passenger motor vehicle\" but does not include \"maxi-cab\", \"motor-cab\", \"tractor\" and \"road-roller\". The expressions \"heavy goods vehicle\", \"heavy passenger motor vehicle\", \"light motor vehicle\", \"medium goods vehicle\", \"medium passenger motor vehicle\", \"maxi-cab\", \"motor-cab\", \"tractor\" and \"road-roller\" shall have the meanings respectively assigned to them in section 2 of the Motor Vehicles Act, 1988 (59 of 1988).";

    private const string Note7 = "\"Computer software\" means any computer program recorded on any disc, tape, perforated media or other information storage device.";

    private const string Note8 = "Machinery and plant includes pipes needed for delivery from the source of supply of raw water to the plant and from the plant to the storage facility.";

    private const string Note9 = "\"Speed boat\" means a motor boat driven by a high speed internal combustion engine capable of propelling the boat at a speed exceeding 24 kilometres per hour in still water and so designed that when running at a speed, it will plane, i.e., its bow will rise from the water.";

    private static readonly DateOnly FirstTaxYear = new(2026, 4, 1);

    [Theory]
    [InlineData("A.I.1", AssetPart.TangibleAssets, AssetItem.Building, 5)]
    [InlineData("A.I.2", AssetPart.TangibleAssets, AssetItem.Building, 10)]
    [InlineData("A.I.3", AssetPart.TangibleAssets, AssetItem.Building, 40)]
    [InlineData("A.I.4", AssetPart.TangibleAssets, AssetItem.Building, 40)]
    [InlineData("A.II", AssetPart.TangibleAssets, AssetItem.FurnitureAndFittings, 10)]
    [InlineData("A.III.1", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 15)]
    [InlineData("A.III.2(i)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 15)]
    [InlineData("A.III.2(ii)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 30)]
    [InlineData("A.III.3(i)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(ii)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 30)]
    [InlineData("A.III.3(iii)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 45)]
    [InlineData("A.III.3(iv)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(v)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 30)]
    [InlineData("A.III.3(vi)(a)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vi)(b)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vi)(c)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vi)(d)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vi)(e)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(a)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(b)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(c)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(d)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(e)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(f)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(g)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(h)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(i)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(j)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(k)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(l)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(m)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(n)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(o)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(p)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(vii)(q)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(viii)(a)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(viii)(b)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(ix)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 30)]
    [InlineData("A.III.3(x)(a)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(x)(b)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(x)(c)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(x)(d)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(x)(e)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(x)(f)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(x)(g)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(x)(h)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(x)(i)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(x)(j)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(x)(k)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(x)(l)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(x)(m)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(x)(n)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.3(x)(o)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.4", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.5", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.6", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(i)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ii)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(iii)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(iv)(a)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(iv)(b)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(v)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(vi)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(vii)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(viii)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)A(a)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)A(b)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)A(c)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)A(d)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)B(a)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)B(b)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)B(c)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)B(d)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)B(e)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)B(f)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)B(g)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)B(h)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)C(a)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)C(b)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)C(c)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)C(d)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)D(a)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)D(b)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)D(c)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)D(d)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)E(a)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)E(b)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)E(c)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)E(d)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)E(e)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)E(f)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)E(g)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)E(h)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)E(i)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)E(j)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)E(k)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)E(l)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)F(a)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)F(b)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)F(c)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)G(a)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)G(b)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)G(c)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)G(d)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)G(e)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)G(f)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)G(g)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(ix)G(h)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(x)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xi)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xii)(a)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xii)(b)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xii)(c)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 15)]
    [InlineData("A.III.7(xiii)(a)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(b)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(c)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(d)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(e)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(f)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(g)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(h)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(i)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(j)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(k)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(l)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(m)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(n)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(o)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(p)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(q)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.7(xiii)(r)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.8(i)(a)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.8(i)(b)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.III.8(ii)", AssetPart.TangibleAssets, AssetItem.MachineryAndPlant, 40)]
    [InlineData("A.IV(1)", AssetPart.TangibleAssets, AssetItem.Ships, 20)]
    [InlineData("A.IV(2)", AssetPart.TangibleAssets, AssetItem.Ships, 20)]
    [InlineData("A.IV(3)", AssetPart.TangibleAssets, AssetItem.Ships, 20)]
    [InlineData("B", AssetPart.IntangibleAssets, null, 25)]
    public async Task Rate_OfEachEntryOfAppendixI_IsTheRateTheTablePrints(string entry, AssetPart part, AssetItem? item, int rate)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var rates = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<DepreciationRates>(FirstTaxYear, TestContext.Current.CancellationToken))!.Rates;

        var found = Assert.Single(rates, candidate => candidate.Entry == entry);
        Assert.Equal((part, item, new Percentage(rate)), (found.Part, found.Item, found.Rate));
        Assert.StartsWith("Income-tax Rules, 2026, Appendix I, Part ", await SourceAsync(scope, Rates, entry), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rates_OfTheFirstTaxYear_AreTheTablesHundredAndThirtyEightPrintedRatesUnderTheirItems()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var rates = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<DepreciationRates>(FirstTaxYear, TestContext.Current.CancellationToken))!.Rates;

        Assert.Equal(138, rates.Count);
        Assert.Equal(4, rates.Count(rate => rate.Item == AssetItem.Building));
        Assert.Equal("A.II", Assert.Single(rates, rate => rate.Item == AssetItem.FurnitureAndFittings).Entry);
        Assert.Equal(129, rates.Count(rate => rate.Item == AssetItem.MachineryAndPlant));
        Assert.Equal(3, rates.Count(rate => rate.Item == AssetItem.Ships));
        Assert.Equal("B", Assert.Single(rates, rate => rate.Part == AssetPart.IntangibleAssets && rate.Item is null).Entry);
        Assert.Equal(123, rates.Count(rate => rate.Rate == new Percentage(40m)));
    }

    [Theory]
    [InlineData("1", Note1)]
    [InlineData("2", Note2)]
    [InlineData("3", Note3)]
    [InlineData("4", Note4)]
    [InlineData("5", Note5)]
    [InlineData("6", Note6)]
    [InlineData("7", Note7)]
    [InlineData("8", Note8)]
    [InlineData("9", Note9)]
    public async Task Note_BelowTheTable_IsInTheTablesWords(string note, string text)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var notes = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<DepreciationNotes>(FirstTaxYear, TestContext.Current.CancellationToken))!.Notes;

        Assert.Equal(text, Assert.Single(notes, candidate => candidate.Note == note).Text);
        Assert.Equal($"Income-tax Rules, 2026, Appendix I, Note {note}", await SourceAsync(scope, Notes, note));
    }

    [Theory]
    [InlineData("half-rate", 50, 180, "restricted to 50% of the prescribed rate", "for less than one hundred and eighty days in that tax year", "Income-tax Act, 2025, section 33(4)")]
    [InlineData("additional", 20, null, "(a) 20% of the actual cost of the new machinery or plant", "in the tax year when it is acquired and put to use", "Income-tax Act, 2025, section 33(9)(a), read with section 33(8)")]
    [InlineData("additional-short-use", 10, 180, "(b) 10% of the actual cost", "for less than one hundred and eighty days in the relevant tax year", "Income-tax Act, 2025, section 33(9)(b), read with section 33(8)")]
    [InlineData("additional-balance", 10, 180, "and 10% of the actual cost", "for less than one hundred and eighty days in the relevant tax year, and 10% of the actual cost shall be allowed in the immediately succeeding tax year.", "Income-tax Act, 2025, section 33(9)(b), read with section 33(8)")]
    [InlineData("cap-domestic-company", 40, null, CapOf40Percent, "(a) section 199(3); or (b) section 200(5); or (c) section 201(2).", "Income-tax Rules, 2026, rule 25(2) [Table: Sl. No. 1]")]
    [InlineData("cap-section-202", 40, null, CapOf40Percent, "Whose income is chargeable to tax under section 202(1).", "Income-tax Rules, 2026, rule 25(2) [Table: Sl. No. 2]")]
    [InlineData("cap-cooperative-society", 40, null, CapOf40Percent, "Cooperative society resident in India", "Income-tax Rules, 2026, rule 25(2) [Table: Sl. No. 3]")]
    [InlineData("laboratory-technology", 40, null, "at the rate of 40% of written down value", "installed during the tax year commencing on or after the 1st April, 1987", "Income-tax Rules, 2026, rule 25(7)")]
    public async Task Allowance_BesideTheTable_IsThePercentageAndTestItsInstrumentSets(string allowance, int percent, int? days, string percentWords, string conditionWords, string source)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var allowances = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<DepreciationAllowances>(FirstTaxYear, TestContext.Current.CancellationToken))!.Allowances;

        var found = Assert.Single(allowances, candidate => candidate.Allowance == allowance);
        Assert.Equal((new Percentage(percent), days), (found.Percent, found.Days));
        Assert.Contains(percentWords, found.Test, StringComparison.Ordinal);
        Assert.Contains(conditionWords, found.Test, StringComparison.Ordinal);
        Assert.Equal(source, await SourceAsync(scope, Allowances, allowance));
    }

    [Fact]
    public async Task Allowances_Description_LeavesWhoMayClaimAdditionalDepreciationAndTheTotalIncomeComputedWithoutItToTheAct()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(Allowances, FirstTaxYear, TestContext.Current.CancellationToken);

        Assert.EndsWith("Who may claim additional depreciation is set by section 33(8), and the total income for the purposes of sections 199(1)(c)(i)(C), 200(1)(a)(iii), 201(3)(a)(iii), 202(1), 203(1)(a)(ii) and 204(3)(a)(ii) is computed without any deduction under it (sections 205(1)(a) and 202(2)(a)(vi)); neither is repeated here.", set!.Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Rates, 138, new[] { RulesForTheTable, ActForTheTable })]
    [InlineData(Notes, 9, new[] { RulesForTheNotes })]
    [InlineData(Allowances, 8, new[] { ActForTheAllowances, RulesForTheAllowances })]
    public async Task Version_OfEachSet_IsTheFirstTaxYearAloneAndAddsEveryEntry(string key, int entries, string[] instruments)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, new DateOnly(2026, 11, 8), TestContext.Current.CancellationToken);

        Assert.NotNull(set);
        Assert.Equal((ParameterSetBasis.Law, ParameterAsOf.TaxPeriodStart), (set.Basis, set.AsOf));
        var version = Assert.Single(set.Versions);
        Assert.Equal((FirstTaxYear, (DateOnly?)null, entries, 0, 0), (version.From, version.Until, version.AddedCount, version.ChangedCount, version.RemovedCount));
        Assert.Equal(instruments, version.Instruments);
    }

    [Fact]
    public async Task Sets_OnTheDayBeforeTheFirstTaxYear_HoldNothing()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();
        var day = new DateOnly(2026, 3, 31);

        Assert.Null(await store.GetAsOfAsync<DepreciationRates>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<DepreciationNotes>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<DepreciationAllowances>(day, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Sets_InTheNextTaxYear_AreStillTheFirstVersionWithNoEnd()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();
        var day = new DateOnly(2027, 4, 1);

        var rates = await store.GetAsOfAsync<DepreciationRates>(day, TestContext.Current.CancellationToken);
        var notes = await store.GetAsOfAsync<DepreciationNotes>(day, TestContext.Current.CancellationToken);
        var allowances = await store.GetAsOfAsync<DepreciationAllowances>(day, TestContext.Current.CancellationToken);

        Assert.Equal((FirstTaxYear, (DateOnly?)null, 138), (rates!.Version.From, rates.Version.Until, rates.Rates.Count));
        Assert.Equal((FirstTaxYear, (DateOnly?)null, 9), (notes!.Version.From, notes.Version.Until, notes.Notes.Count));
        Assert.Equal((FirstTaxYear, (DateOnly?)null, 8), (allowances!.Version.From, allowances.Version.Until, allowances.Allowances.Count));
    }

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string key, string rowKey)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, FirstTaxYear, TestContext.Current.CancellationToken);

        return Assert.Single(view!.Rows, row => row.Key == rowKey).Source;
    }
}
