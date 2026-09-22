using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Kernel.Time;

public sealed class FinancialYearTests
{
    private static readonly FinancialYear Year2026 = new(2026);

    [Theory]
    [InlineData(1)]
    [InlineData(2026)]
    [InlineData(9998)]
    public void Constructor_StartYearWithinBounds_ExposesTheStartYear(int startYear)
    {
        var year = new FinancialYear(startYear);

        Assert.Equal(startYear, year.StartYear);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(9999)]
    [InlineData(int.MaxValue)]
    public void Constructor_StartYearOutsideBounds_ThrowsNamingTheStartYear(int startYear)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new FinancialYear(startYear));

        Assert.Equal("startYear", exception.ParamName);
        Assert.Equal(startYear, exception.ActualValue);
    }

    [Theory]
    [InlineData("2026-03-31", 2025)]
    [InlineData("2026-04-01", 2026)]
    [InlineData("2026-12-31", 2026)]
    [InlineData("2027-01-01", 2026)]
    [InlineData("2028-02-29", 2027)]
    [InlineData("2027-03-31", 2026)]
    public void FromDate_Date_StartsInTheCalendarYearOfTheLatestFirstOfApril(string date, int expectedStartYear)
    {
        var year = FinancialYear.FromDate(Date(date));

        Assert.Equal(new FinancialYear(expectedStartYear), year);
    }

    [Fact]
    public void StartAndEnd_Year_RunFromFirstOfAprilToThirtyFirstOfMarch()
    {
        Assert.Equal(new DateOnly(2026, 4, 1), Year2026.Start);
        Assert.Equal(new DateOnly(2027, 3, 31), Year2026.End);
        Assert.Equal(4, FinancialYear.StartMonth);
        Assert.Equal(1, FinancialYear.StartDay);
    }

    [Theory]
    [InlineData(2026, "2026-27", "2027-28")]
    [InlineData(1999, "1999-00", "2000-01")]
    [InlineData(2099, "2099-00", "2100-01")]
    [InlineData(1, "0001-02", "0002-03")]
    [InlineData(9998, "9998-99", "9999-00")]
    public void Labels_Year_UseTheStartYearAndTheTwoDigitEndYear(int startYear, string label, string assessmentYearLabel)
    {
        var year = new FinancialYear(startYear);

        Assert.Equal(label, year.Label);
        Assert.Equal(assessmentYearLabel, year.AssessmentYearLabel);
        Assert.Equal(label, year.ToString());
    }

    [Fact]
    public void NextAndPrevious_Year_StepTheStartYear()
    {
        Assert.Equal(new FinancialYear(2027), Year2026.Next);
        Assert.Equal(new FinancialYear(2025), Year2026.Previous);
        Assert.Equal(Year2026, Year2026.Next.Previous);
        Assert.Equal(Year2026.End.AddDays(1), Year2026.Next.Start);
        Assert.Equal(Year2026.Start.AddDays(-1), Year2026.Previous.End);
    }

    [Theory]
    [InlineData("2026-04-01", true)]
    [InlineData("2027-03-31", true)]
    [InlineData("2026-10-15", true)]
    [InlineData("2026-03-31", false)]
    [InlineData("2027-04-01", false)]
    public void Contains_Date_IsTrueOnlyInsideTheYearInclusive(string date, bool expected)
    {
        Assert.Equal(expected, Year2026.Contains(Date(date)));
    }

    [Fact]
    public void Quarters_Year_AreFourContiguousQuartersCoveringTheYear()
    {
        var quarters = Year2026.Quarters;

        Assert.Equal(4, quarters.Count);
        Assert.Equal([1, 2, 3, 4], quarters.Select(quarter => quarter.Number));
        Assert.All(quarters, quarter => Assert.Equal(Year2026, quarter.Year));
        Assert.Equal(Year2026.Start, quarters[0].Start);
        Assert.Equal(Year2026.End, quarters[3].End);
        Assert.All(quarters.Zip(quarters.Skip(1)), pair => Assert.Equal(pair.First.End.AddDays(1), pair.Second.Start));
    }

    [Theory]
    [InlineData("2026-04-01", 1)]
    [InlineData("2026-06-30", 1)]
    [InlineData("2026-07-01", 2)]
    [InlineData("2026-09-30", 2)]
    [InlineData("2026-10-01", 3)]
    [InlineData("2026-12-31", 3)]
    [InlineData("2027-01-01", 4)]
    [InlineData("2027-03-31", 4)]
    public void QuarterOf_DateInsideTheYear_ReturnsTheQuarterHoldingIt(string date, int expectedNumber)
    {
        var quarter = Year2026.QuarterOf(Date(date));

        Assert.Equal(new FinancialQuarter(Year2026, expectedNumber), quarter);
        Assert.True(quarter.Contains(Date(date)));
    }

    [Theory]
    [InlineData("2026-03-31")]
    [InlineData("2027-04-01")]
    [InlineData("2020-01-01")]
    public void QuarterOf_DateOutsideTheYear_ThrowsNamingTheDateAndTheYear(string date)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Year2026.QuarterOf(Date(date)));

        Assert.Equal("date", exception.ParamName);
        Assert.Contains(date, exception.Message, StringComparison.Ordinal);
        Assert.Contains("2026-27", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Comparisons_Years_FollowTheStartYear()
    {
        var earlier = new FinancialYear(2025);

        Assert.True(earlier < Year2026);
        Assert.True(Year2026 > earlier);
        Assert.True(earlier <= Year2026);
        Assert.True(Year2026 >= earlier);
        Assert.True(Year2026 <= new FinancialYear(2026));
        Assert.True(Year2026 >= new FinancialYear(2026));
        Assert.False(Year2026 < earlier);
        Assert.False(earlier > Year2026);
        Assert.True(earlier.CompareTo(Year2026) < 0);
        Assert.True(Year2026.CompareTo(earlier) > 0);
        Assert.Equal(0, Year2026.CompareTo(new FinancialYear(2026)));
    }

    [Theory]
    [InlineData("2026-27", 2026)]
    [InlineData("1999-00", 1999)]
    [InlineData("0001-02", 1)]
    [InlineData("9998-99", 9998)]
    public void Parse_Label_ReturnsTheYear(string label, int expectedStartYear)
    {
        var parsed = FinancialYear.Parse(label, CultureInfo.InvariantCulture);
        var tried = FinancialYear.TryParse(label, CultureInfo.InvariantCulture, out var result);

        Assert.Equal(new FinancialYear(expectedStartYear), parsed);
        Assert.True(tried);
        Assert.Equal(parsed, result);
    }

    [Theory]
    [InlineData("2026-28")]
    [InlineData("2026-26")]
    [InlineData("26-27")]
    [InlineData("2026/27")]
    [InlineData("2026-2027")]
    [InlineData("2026-2")]
    [InlineData(" 2026-27")]
    [InlineData("2026-27 ")]
    [InlineData("0000-01")]
    [InlineData("9999-00")]
    [InlineData("+026-27")]
    [InlineData("2026-x7")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_MalformedLabel_ReturnsFalseWithTheDefault(string? label)
    {
        var tried = FinancialYear.TryParse(label, CultureInfo.InvariantCulture, out var result);

        Assert.False(tried);
        Assert.Equal(default, result);
    }

    [Fact]
    public void Parse_MalformedLabel_ThrowsFormatExceptionNamingTheLabel()
    {
        var exception = Assert.Throws<FormatException>(() => FinancialYear.Parse("2026-28", CultureInfo.InvariantCulture));

        Assert.Contains("'2026-28'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-03-31T18:29:59Z", 2025)]
    [InlineData("2026-03-31T18:30:00Z", 2026)]
    [InlineData("2027-03-31T18:30:00Z", 2027)]
    public void Current_ClockAtTheIstDayBoundary_UsesTheIndianDate(string utcNow, int expectedStartYear)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture));

        var current = FinancialYear.Current(timeProvider);

        Assert.Equal(new FinancialYear(expectedStartYear), current);
    }

    [Fact]
    public void Equals_SameStartYear_IsEqualAndSharesTheHashCode()
    {
        var other = new FinancialYear(2026);

        Assert.Equal(Year2026, other);
        Assert.True(Year2026 == other);
        Assert.Equal(Year2026.GetHashCode(), other.GetHashCode());
        Assert.NotEqual(Year2026, Year2026.Next);
    }

    private static DateOnly Date(string text) => DateOnly.Parse(text, CultureInfo.InvariantCulture);
}
