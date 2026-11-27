using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Approvals;

// Each case names the instant a step starts and the midnight in India it is due by, both in India's own time, so the day a step
// starts on is read as a person there reads it.
public sealed class WorkingDaysTests
{
    [Theory]
    [InlineData("2026-11-23T10:00:00+05:30", 1, "2026-11-25T00:00:00+05:30")]
    [InlineData("2026-11-23T10:00:00+05:30", 2, "2026-11-26T00:00:00+05:30")]
    [InlineData("2026-11-23T00:00:00+05:30", 1, "2026-11-25T00:00:00+05:30")]
    [InlineData("2026-11-26T23:59:59+05:30", 1, "2026-11-28T00:00:00+05:30")]
    [InlineData("2026-11-27T00:00:00+05:30", 1, "2026-12-01T00:00:00+05:30")]
    [InlineData("2026-11-27T23:59:59+05:30", 1, "2026-12-01T00:00:00+05:30")]
    [InlineData("2026-11-28T00:00:00+05:30", 1, "2026-12-01T00:00:00+05:30")]
    [InlineData("2026-11-29T15:00:00+05:30", 1, "2026-12-01T00:00:00+05:30")]
    [InlineData("2026-11-27T10:00:00+05:30", 5, "2026-12-05T00:00:00+05:30")]
    [InlineData("2026-03-31T18:00:00+05:30", 1, "2026-04-02T00:00:00+05:30")]
    [InlineData("2028-02-28T09:00:00+05:30", 1, "2028-03-01T00:00:00+05:30")]
    [InlineData("2028-02-29T09:00:00+05:30", 1, "2028-03-02T00:00:00+05:30")]
    [InlineData("2026-11-23T10:00:00+05:30", 30, "2027-01-05T00:00:00+05:30")]
    public void DueAt_AStartAndItsWorkingDays_IsTheMidnightInIndiaAfterTheLastOfThem(string startedAt, int workingDays, string dueAt)
    {
        var due = WorkingDays.DueAt(Instant(startedAt), workingDays);

        Assert.Equal(Instant(dueAt), due);
        Assert.Equal(TimeSpan.Zero, due.Offset);
    }

    [Theory]
    [InlineData("2026-11-26T18:29:59Z", "2026-11-28T00:00:00+05:30")]
    [InlineData("2026-11-26T18:30:00Z", "2026-12-01T00:00:00+05:30")]
    public void DueAt_AStartJustBeforeAndAtMidnightInIndia_CountsFromTheDayInIndia(string startedAt, string dueAt)
    {
        Assert.Equal(Instant(dueAt), WorkingDays.DueAt(Instant(startedAt), 1));
    }

    [Fact]
    public void DueAt_NoWorkingDays_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkingDays.DueAt(Instant("2026-11-23T10:00:00+05:30"), 0));
    }

    private static DateTimeOffset Instant(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.None);
}
