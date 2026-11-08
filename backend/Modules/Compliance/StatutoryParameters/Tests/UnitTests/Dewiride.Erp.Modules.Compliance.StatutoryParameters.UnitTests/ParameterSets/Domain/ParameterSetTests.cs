using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.ParameterSets.Domain;

public sealed class ParameterSetTests
{
    private static readonly ParameterColumnSet Columns = TestSets.RateSet().Columns;

    [Theory]
    [InlineData("gst.sac-codes")]
    [InlineData("gst.central-tax-rates")]
    [InlineData("direct-tax.tds.sections-2025")]
    [InlineData("a.b")]
    public void Create_WellFormedKey_KeepsItUnderAVersion7Id(string key)
    {
        var set = ParameterSet.Create(key, "Probe set", "A set the tests build.", SetBasis.Interpretation, AsOfDate.TaxPeriodStart, Columns);

        Assert.Equal(key, set.Key);
        Assert.Equal((SetBasis.Interpretation, AsOfDate.TaxPeriodStart), (set.Basis, set.AsOf));
        Assert.Equal(7, set.Id.Value.Version);
    }

    [Theory]
    [InlineData("gst")]
    [InlineData("GST.sac-codes")]
    [InlineData("gst.")]
    [InlineData(".gst")]
    [InlineData("gst..codes")]
    [InlineData("gst.codes-")]
    [InlineData("gst.-codes")]
    [InlineData("gst.sac_codes")]
    [InlineData("gst.sac-codes\n")]
    [InlineData("gst.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghi")]
    public void Create_KeyOffThePattern_IsRefused(string key)
    {
        Assert.Throws<ArgumentException>(() => ParameterSet.Create(key, "Probe set", "A set the tests build.", SetBasis.Law, AsOfDate.SupplyDate, Columns));
    }

    [Fact]
    public void IsKey_KeyOfSixtyFourCharacters_IsTrueAndOfSixtyFive_IsFalse()
    {
        Assert.True(ParameterSet.IsKey("gst." + new string('a', ParameterSet.KeyMaxLength - 4)));
        Assert.False(ParameterSet.IsKey("gst." + new string('a', ParameterSet.KeyMaxLength - 3)));
    }

    [Theory]
    [InlineData("", "A set the tests build.")]
    [InlineData("Probe set ", "A set the tests build.")]
    [InlineData("Probe set", "")]
    [InlineData("Probe set", "A set  the tests build.")]
    public void Create_TitleOrDescriptionOutOfShape_IsRefused(string title, string description)
    {
        Assert.Throws<ArgumentException>(() => ParameterSet.Create("test.probe-set", title, description, SetBasis.Law, AsOfDate.SupplyDate, Columns));
    }

    [Fact]
    public void Revise_NewTitleAndColumns_KeepsTheKeyAndTakesTheRest()
    {
        var set = TestSets.RateSet();
        var columns = ParameterColumnSet.Create([TestSets.Column("entry", CellKind.Code, isKey: true)]);

        set.Revise("Another title", "Another description.", SetBasis.Interpretation, AsOfDate.TaxPeriodStart, columns);

        Assert.Equal(("test.probe-set", "Another title", "Another description.", columns), (set.Key, set.Title, set.Description, set.Columns));
    }

    [Fact]
    public void CreateVersion_InstrumentsCited_KeepsThemInTheirOrder()
    {
        var version = ParameterVersion.Create(TestSets.RateSet(), TestSets.Day("2019-10-01"), ["Second instrument", "First instrument"]);

        Assert.Equal(["Second instrument", "First instrument"], version.Instruments);
        Assert.Equal(7, version.Id.Value.Version);
    }

    [Theory]
    [InlineData("2014-04-01")]
    [InlineData("2026-04-01")]
    public void CreateVersion_OfASetDatedByFinancialYearOnThe1stApril_IsCreated(string from)
    {
        var version = ParameterVersion.Create(FinancialYearSet(), TestSets.Day(from), ["An instrument"]);

        Assert.Equal(TestSets.Day(from), version.From);
    }

    [Theory]
    [InlineData("2014-08-29")]
    [InlineData("2016-03-31")]
    [InlineData("2026-01-01")]
    public void CreateVersion_OfASetDatedByFinancialYearOnAnotherDay_IsRefused(string from)
    {
        Assert.Throws<ArgumentException>(() => ParameterVersion.Create(FinancialYearSet(), TestSets.Day(from), ["An instrument"]));
    }

    [Fact]
    public void CreateVersion_OfASetDatedByDeduction_TakesAnyDay()
    {
        var set = TestSets.Set(AsOfDate.DeductionDate, TestSets.Column("entry", CellKind.Code, isKey: true));

        Assert.Equal(TestSets.Day("2026-10-01"), ParameterVersion.Create(set, TestSets.Day("2026-10-01"), ["An instrument"]).From);
    }

    [Fact]
    public void CreateVersion_NoInstrument_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => ParameterVersion.Create(TestSets.RateSet(), TestSets.Day("2019-10-01"), []));
    }

    [Fact]
    public void CreateVersion_InstrumentCitedTwice_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => ParameterVersion.Create(TestSets.RateSet(), TestSets.Day("2019-10-01"), ["An instrument", "An instrument"]));
    }

    [Fact]
    public void CreateVersion_InstrumentLongerThanFourHundredCharacters_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => ParameterVersion.Create(TestSets.RateSet(), TestSets.Day("2019-10-01"), [new string('a', ParameterVersion.InstrumentMaxLength + 1)]));
    }

    [Fact]
    public void CreateRow_CellsThatFit_TakesItsKeyFromTheKeyColumn()
    {
        var set = TestSets.RateSet();

        var row = TestSets.Rate(set, "21(ii)", "2019-10-01", "2025-09-21");

        Assert.Equal(("21(ii)", set.Id, TestSets.Day("2019-10-01"), TestSets.Day("2025-09-21")), (row.RowKey, row.SetId, row.From, row.Until));
    }

    [Fact]
    public void CreateRow_CellThatBreaksItsColumn_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => TestSets.Rate(TestSets.RateSet(), "21(ii)", "2019-10-01", null, "9.00001"));
    }

    [Fact]
    public void CreateRow_EndingBeforeItStarts_IsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TestSets.Rate(TestSets.RateSet(), "21(ii)", "2019-10-01", "2019-09-30"));
    }

    [Fact]
    public void CreateRow_SourceOutOfShape_IsRefused()
    {
        var set = TestSets.RateSet();

        Assert.Throws<ArgumentException>(() => ParameterRow.Create(set, TestSets.Day("2019-10-01"), null, TestSets.Cells(("entry", "21(ii)"), ("rate", "9"), ("note", null)), " "));
    }

    [Fact]
    public void ReviseRow_CellsOfAnotherKey_IsRefused()
    {
        var set = TestSets.RateSet();
        var row = TestSets.Rate(set, "21(ii)", "2019-10-01", null);

        Assert.Throws<ArgumentException>(() => row.Revise(set, null, TestSets.Cells(("entry", "21(i)"), ("rate", "9"), ("note", null)), "Another source"));
    }

    [Fact]
    public void ReviseRow_OfAnotherSet_IsRefused()
    {
        var row = TestSets.Rate(TestSets.RateSet(), "21(ii)", "2019-10-01", null);

        Assert.Throws<ArgumentException>(() => row.Revise(TestSets.RateSet(), null, row.Cells, row.Source));
    }

    [Fact]
    public void ReviseRow_NewEndCellsAndSource_TakesThem()
    {
        var set = TestSets.RateSet();
        var row = TestSets.Rate(set, "21(ii)", "2019-10-01", null);
        var cells = TestSets.Cells(("entry", "21(ii)"), ("rate", "18"), ("note", "A note"));

        row.Revise(set, TestSets.Day("2025-09-21"), cells, "Another source");

        Assert.Equal((TestSets.Day("2025-09-21"), cells, "Another source"), (row.Until, row.Cells, row.Source));
    }

    private static ParameterSet FinancialYearSet() =>
        TestSets.Set(AsOfDate.FinancialYearStart, TestSets.Column("entry", CellKind.Code, isKey: true), TestSets.Column("years", CellKind.Integer));
}
