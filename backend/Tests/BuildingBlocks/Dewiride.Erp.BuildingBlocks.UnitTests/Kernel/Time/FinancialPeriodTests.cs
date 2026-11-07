using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Kernel.Time;

public sealed class FinancialPeriodTests
{
    private static readonly FinancialYear Year2026 = new(2026);

    [Theory]
    [InlineData(1, "2026-04-01", "2026-04-30")]
    [InlineData(2, "2026-05-01", "2026-05-31")]
    [InlineData(9, "2026-12-01", "2026-12-31")]
    [InlineData(10, "2027-01-01", "2027-01-31")]
    [InlineData(11, "2027-02-01", "2027-02-28")]
    [InlineData(12, "2027-03-01", "2027-03-31")]
    public void StartAndEnd_Period_CoverOneCalendarMonthCountedFromApril(int number, string start, string end)
    {
        var period = new FinancialPeriod(Year2026, number);

        Assert.Equal(number, period.Number);
        Assert.Equal(Year2026, period.Year);
        Assert.Equal(Date(start), period.Start);
        Assert.Equal(Date(end), period.End);
    }

    [Fact]
    public void End_FebruaryOfALeapYear_IsTheTwentyNinth()
    {
        var february = new FinancialPeriod(new FinancialYear(2027), 11);

        Assert.Equal(new DateOnly(2028, 2, 1), february.Start);
        Assert.Equal(new DateOnly(2028, 2, 29), february.End);
    }

    [Theory]
    [InlineData(1, "April 2026")]
    [InlineData(9, "December 2026")]
    [InlineData(10, "January 2027")]
    [InlineData(12, "March 2027")]
    public void Label_Period_IsTheMonthAndItsCalendarYear(int number, string expected)
    {
        var period = new FinancialPeriod(Year2026, number);

        Assert.Equal(expected, period.Label);
        Assert.Equal(expected, period.ToString());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(7, 3)]
    [InlineData(10, 4)]
    [InlineData(12, 4)]
    public void Quarter_Period_IsTheQuarterHoldingItsMonth(int number, int expectedQuarter)
    {
        var period = new FinancialPeriod(Year2026, number);

        Assert.Equal(new FinancialQuarter(Year2026, expectedQuarter), period.Quarter);
        Assert.True(period.Quarter.Contains(period.Start));
        Assert.True(period.Quarter.Contains(period.End));
    }

    [Theory]
    [InlineData("2027-03-01", true)]
    [InlineData("2027-03-15", true)]
    [InlineData("2027-03-31", true)]
    [InlineData("2027-02-28", false)]
    [InlineData("2027-04-01", false)]
    public void Contains_DateAgainstTheLastPeriod_IsTrueOnlyInsideItInclusive(string date, bool expected)
    {
        var march = new FinancialPeriod(Year2026, 12);

        Assert.Equal(expected, march.Contains(Date(date)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void Constructor_NumberOutsideOneToTwelve_ThrowsNamingTheNumber(int number)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new FinancialPeriod(Year2026, number));

        Assert.Equal("number", exception.ParamName);
        Assert.Equal(number, exception.ActualValue);
    }

    [Fact]
    public void Equals_SameYearAndNumber_IsEqualToTheYearsPeriod()
    {
        var june = new FinancialPeriod(Year2026, 3);

        Assert.Equal(Year2026.Periods[2], june);
        Assert.True(Year2026.Periods[2] == june);
        Assert.NotEqual(new FinancialPeriod(Year2026, 4), june);
        Assert.NotEqual(new FinancialPeriod(Year2026.Next, 3), june);
    }

    private static DateOnly Date(string text) => DateOnly.Parse(text, CultureInfo.InvariantCulture);
}
