using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Kernel.Time;

public sealed class FinancialQuarterTests
{
    private static readonly FinancialYear Year2026 = new(2026);

    [Theory]
    [InlineData(1, "2026-04-01", "2026-06-30")]
    [InlineData(2, "2026-07-01", "2026-09-30")]
    [InlineData(3, "2026-10-01", "2026-12-31")]
    [InlineData(4, "2027-01-01", "2027-03-31")]
    public void StartAndEnd_Quarter_CoverThreeCalendarMonthsFromApril(int number, string start, string end)
    {
        var quarter = new FinancialQuarter(Year2026, number);

        Assert.Equal(number, quarter.Number);
        Assert.Equal(Year2026, quarter.Year);
        Assert.Equal(Date(start), quarter.Start);
        Assert.Equal(Date(end), quarter.End);
    }

    [Theory]
    [InlineData(1, "Q1 2026-27")]
    [InlineData(3, "Q3 2026-27")]
    [InlineData(4, "Q4 2026-27")]
    public void Label_Quarter_IsTheNumberAndTheYearLabel(int number, string expected)
    {
        var quarter = new FinancialQuarter(Year2026, number);

        Assert.Equal(expected, quarter.Label);
        Assert.Equal(expected, quarter.ToString());
    }

    [Theory]
    [InlineData("2027-01-01", true)]
    [InlineData("2027-02-14", true)]
    [InlineData("2027-03-31", true)]
    [InlineData("2026-12-31", false)]
    [InlineData("2027-04-01", false)]
    public void Contains_DateAgainstTheFourthQuarter_IsTrueOnlyInsideItInclusive(string date, bool expected)
    {
        var fourth = new FinancialQuarter(Year2026, 4);

        Assert.Equal(expected, fourth.Contains(Date(date)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public void Constructor_NumberOutsideOneToFour_ThrowsNamingTheNumber(int number)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new FinancialQuarter(Year2026, number));

        Assert.Equal("number", exception.ParamName);
        Assert.Equal(number, exception.ActualValue);
    }

    [Fact]
    public void Equals_SameYearAndNumber_IsEqualToTheYearsQuarter()
    {
        var second = new FinancialQuarter(Year2026, 2);

        Assert.Equal(Year2026.Quarters[1], second);
        Assert.True(Year2026.Quarters[1] == second);
        Assert.NotEqual(new FinancialQuarter(Year2026, 3), second);
        Assert.NotEqual(new FinancialQuarter(Year2026.Next, 2), second);
    }

    private static DateOnly Date(string text) => DateOnly.Parse(text, CultureInfo.InvariantCulture);
}
