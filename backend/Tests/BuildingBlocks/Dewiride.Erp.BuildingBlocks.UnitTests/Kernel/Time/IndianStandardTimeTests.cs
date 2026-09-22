using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Kernel.Time;

public sealed class IndianStandardTimeTests
{
    [Fact]
    public void Offset_IsFiveHoursThirtyMinutesAheadOfUtc()
    {
        Assert.Equal(new TimeSpan(5, 30, 0), IndianStandardTime.Offset);
    }

    [Fact]
    public void ToIst_UtcInstant_KeepsTheInstantAndAppliesTheOffset()
    {
        var instant = Instant("2026-03-31T18:30:00Z");

        var ist = IndianStandardTime.ToIst(instant);

        Assert.Equal(IndianStandardTime.Offset, ist.Offset);
        Assert.Equal(instant, ist);
        Assert.Equal(instant.UtcDateTime, ist.UtcDateTime);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Unspecified), ist.DateTime);
    }

    [Fact]
    public void ToIst_InstantWithAnotherOffset_ConvertsThroughUtc()
    {
        var instant = Instant("2026-04-01T02:00:00-08:00");

        var ist = IndianStandardTime.ToIst(instant);

        Assert.Equal(IndianStandardTime.Offset, ist.Offset);
        Assert.Equal(new DateTime(2026, 4, 1, 15, 30, 0, DateTimeKind.Unspecified), ist.DateTime);
    }

    [Theory]
    [InlineData("2026-03-31T18:29:59Z", "2026-03-31")]
    [InlineData("2026-03-31T18:30:00Z", "2026-04-01")]
    [InlineData("2026-12-31T18:30:00Z", "2027-01-01")]
    [InlineData("2026-04-01T02:00:00-08:00", "2026-04-01")]
    [InlineData("2026-04-01T00:00:00+05:30", "2026-04-01")]
    [InlineData("2026-03-31T23:59:59+05:30", "2026-03-31")]
    public void IstDate_Instant_IsTheCalendarDateInIndia(string instant, string expected)
    {
        var date = IndianStandardTime.IstDate(Instant(instant));

        Assert.Equal(Date(expected), date);
    }

    [Fact]
    public void StartOfIstDay_Date_IsMidnightInIndiaWhichIsThePreviousEveningInUtc()
    {
        var start = IndianStandardTime.StartOfIstDay(new DateOnly(2026, 4, 1));

        Assert.Equal(IndianStandardTime.Offset, start.Offset);
        Assert.Equal(new DateTimeOffset(2026, 3, 31, 18, 30, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTime(2026, 3, 31, 18, 30, 0, DateTimeKind.Utc), start.UtcDateTime);
        Assert.Equal(TimeOnly.MinValue, TimeOnly.FromDateTime(start.DateTime));
    }

    [Theory]
    [InlineData("2026-03-31T18:29:59Z", "2026-03-31")]
    [InlineData("2026-03-31T18:30:00Z", "2026-04-01")]
    public void Today_TimeProviderAtTheIstDayBoundary_UsesTheIstDate(string utcNow, string expected)
    {
        var timeProvider = new FakeTimeProvider(Instant(utcNow));

        var today = IndianStandardTime.Today(timeProvider);

        Assert.Equal(Date(expected), today);
    }

    [Fact]
    public void Today_NullTimeProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => IndianStandardTime.Today(null!));
    }

    private static DateTimeOffset Instant(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);

    private static DateOnly Date(string text) => DateOnly.Parse(text, CultureInfo.InvariantCulture);
}
