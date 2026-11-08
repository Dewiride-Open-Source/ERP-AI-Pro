using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Numbering;

public sealed class NumberSeriesYearTests
{
    private const int MaxLength = 14;

    private static readonly NumberFormat Format = new("TI.", NumberYearStyle.Full, ".", 3);

    [Fact]
    public void TryIssue_AnOpenedYear_GivesItsNumbersInTurnFromTheFirst()
    {
        var year = NumberSeriesYear.Open(NumberSeriesId.Create(), new FinancialYear(2026), Format, MaxLength, 41);

        Assert.True(year.TryIssue(out var first));
        Assert.True(year.TryIssue(out var second));

        Assert.Equal((41L, 42L, 43L, true), (first, second, year.NextNumber, year.InUse));
    }

    [Fact]
    public void TryIssue_PastTheLastNumberThatFits_GivesNone()
    {
        var year = NumberSeriesYear.Open(NumberSeriesId.Create(), new FinancialYear(2026), Format, MaxLength, 999);

        Assert.True(year.TryIssue(out var last));
        Assert.False(year.TryIssue(out var none));

        Assert.Equal((999L, 0L, 1000L, 999L), (last, none, year.NextNumber, year.LastNumber));
    }

    [Fact]
    public void Reopen_BeforeTheFirstNumber_TakesTheNewFormatAndFirstNumber()
    {
        var year = NumberSeriesYear.Open(NumberSeriesId.Create(), new FinancialYear(2026), Format, MaxLength, 1);
        var changed = new NumberFormat("TX", NumberYearStyle.None, "", 6);

        year.Reopen(changed, 12, 70);

        Assert.Equal((changed, 12, 70L, 70L, false), (year.Format, year.MaxLength, year.FirstNumber, year.NextNumber, year.InUse));
    }

    [Fact]
    public void Reopen_OnceTheYearGaveANumber_Throws()
    {
        var year = NumberSeriesYear.Open(NumberSeriesId.Create(), new FinancialYear(2026), Format, MaxLength, 1);
        year.TryIssue(out _);

        Assert.Throws<InvalidOperationException>(() => year.Reopen(Format, MaxLength, 1));
    }

    [Fact]
    public void Open_AFirstNumberBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberSeriesYear.Open(NumberSeriesId.Create(), new FinancialYear(2026), Format, MaxLength, 0));
    }

    [Fact]
    public void Create_ANumbering_KeepsItsKindAndFormatUntilChanged()
    {
        var series = NumberSeries.Create("test.numbering.invoices", Format);
        var changed = Format with { Digits = 5 };

        series.Change(changed);

        Assert.Equal(("test.numbering.invoices", changed), (series.DocumentType, series.Format));
        Assert.Throws<ArgumentException>(() => NumberSeries.Create(" ", Format));
    }
}
