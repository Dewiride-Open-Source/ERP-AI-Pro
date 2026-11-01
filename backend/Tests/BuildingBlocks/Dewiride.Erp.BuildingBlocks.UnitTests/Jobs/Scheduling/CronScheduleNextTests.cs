using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Jobs.Scheduling;

// Instants are written in India Standard Time unless they end in Z; 2026-11-01 is a Sunday. The rows of the two day fields follow
// crontab(5) of Ubuntu's cron package: a field that begins with * is unrestricted, so */2 still filters but makes both day fields
// count, while a list that begins with a number is restricted.
public sealed class CronScheduleNextTests
{
    public static TheoryData<string, string, string> Occurrences => new()
    {
        { "30 0 * * *", "2026-11-01T23:59:00+05:30", "2026-11-02T00:30:00+05:30" },
        { "30 0 * * *", "2026-11-02T00:30:00+05:30", "2026-11-03T00:30:00+05:30" },
        { "30 0 * * *", "2026-11-01T19:00:00Z", "2026-11-03T00:30:00+05:30" },
        { "30 0 * * *", "2026-11-02T00:29:59.9999999+05:30", "2026-11-02T00:30:00+05:30" },
        { "0 0 31 * *", "2026-04-15T00:00:00+05:30", "2026-05-31T00:00:00+05:30" },
        { "0 0 31 * *", "2026-05-31T00:00:00+05:30", "2026-07-31T00:00:00+05:30" },
        { "0 0 30 * *", "2027-01-30T00:00:00+05:30", "2027-03-30T00:00:00+05:30" },
        { "0 0 29 2 *", "2026-03-01T00:00:00+05:30", "2028-02-29T00:00:00+05:30" },
        { "0 0 29 2 *", "2096-02-29T00:00:00+05:30", "2104-02-29T00:00:00+05:30" },
        { "59 23 31 12 *", "2026-12-31T23:59:00+05:30", "2027-12-31T23:59:00+05:30" },
        { "0 0 13 * 5", "2026-11-01T00:00:00+05:30", "2026-11-06T00:00:00+05:30" },
        { "0 0 13 * 5", "2026-11-06T00:00:00+05:30", "2026-11-13T00:00:00+05:30" },
        { "0 0 13 * 5", "2026-11-13T00:00:00+05:30", "2026-11-20T00:00:00+05:30" },
        { "0 0 13 * 5", "2026-12-11T00:00:00+05:30", "2026-12-13T00:00:00+05:30" },
        { "0 0 13 * *", "2026-11-13T00:00:00+05:30", "2026-12-13T00:00:00+05:30" },
        { "0 0 * * 5", "2026-11-13T00:00:00+05:30", "2026-11-20T00:00:00+05:30" },
        { "0 0 */2 * 1", "2026-11-01T00:00:00+05:30", "2026-11-09T00:00:00+05:30" },
        { "0 0 1-31/2 * 1", "2026-11-01T00:00:00+05:30", "2026-11-02T00:00:00+05:30" },
        { "0 0 */10 * 1", "2026-11-01T00:00:00+05:30", "2026-12-21T00:00:00+05:30" },
        { "0 0 1,*/10 * 1", "2026-11-01T00:00:00+05:30", "2026-11-02T00:00:00+05:30" },
        { "0 0 1,*/10 * 1", "2026-11-02T00:00:00+05:30", "2026-11-09T00:00:00+05:30" },
        { "0 0 * * 7", "2026-11-01T00:00:00+05:30", "2026-11-08T00:00:00+05:30" },
        { "0 0 * * 5-7", "2026-11-02T00:00:00+05:30", "2026-11-06T00:00:00+05:30" },
        { "0 0 * * 5-7", "2026-11-08T00:00:00+05:30", "2026-11-13T00:00:00+05:30" },
        { "0 9-17/4 * * *", "2026-11-01T09:00:00+05:30", "2026-11-01T13:00:00+05:30" },
        { "0 9-17/4 * * *", "2026-11-01T13:00:00+05:30", "2026-11-01T17:00:00+05:30" },
        { "0 9-17/4 * * *", "2026-11-01T17:00:00+05:30", "2026-11-02T09:00:00+05:30" },
        { "* * * * *", "2026-11-01T12:00:30+05:30", "2026-11-01T12:01:00+05:30" },
        { "0 0 * * 1", "2026-12-31T00:00:00+05:30", "2027-01-04T00:00:00+05:30" },
        { "0 0 31 4-12/2 *", "2026-04-01T00:00:00+05:30", "2026-08-31T00:00:00+05:30" },
        { "0 0 29 2 */7", "2026-11-01T00:00:00+05:30", "2032-02-29T00:00:00+05:30" },
        { "0 0 29 2 */7", "2088-03-01T00:00:00+05:30", "2128-02-29T00:00:00+05:30" },
    };

    [Theory]
    [MemberData(nameof(Occurrences))]
    public void NextAfter_Instant_IsTheFirstMatchingMinuteStrictlyAfterIt(string expression, string after, string next)
    {
        var schedule = CronSchedule.Parse(expression).Value;

        var occurrence = schedule.NextAfter(DateTimeOffset.Parse(after, CultureInfo.InvariantCulture));

        Assert.Equal(DateTimeOffset.Parse(next, CultureInfo.InvariantCulture), occurrence);
        Assert.Equal(TimeSpan.Zero, occurrence.Offset);
    }

    [Fact]
    public void NextAfter_SundayAsZeroOrSeven_FindsTheSameDays()
    {
        var after = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.FromHours(5.5));

        Assert.Equal(CronSchedule.Parse("0 0 * * 0").Value.NextAfter(after), CronSchedule.Parse("0 0 * * 7").Value.NextAfter(after));
    }
}
