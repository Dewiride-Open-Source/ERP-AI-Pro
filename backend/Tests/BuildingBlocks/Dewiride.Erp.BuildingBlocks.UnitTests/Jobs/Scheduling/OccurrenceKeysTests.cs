using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Jobs.Scheduling;

public sealed class OccurrenceKeysTests
{
    [Theory]
    [InlineData("2026-11-01T19:00:00Z", "occurrence:2026-11-02T00:30+05:30")]
    [InlineData("2026-11-02T00:30:00+05:30", "occurrence:2026-11-02T00:30+05:30")]
    [InlineData("2027-03-31T18:30:00Z", "occurrence:2027-04-01T00:00+05:30")]
    public void For_Occurrence_NamesItsMinuteInIndiaTime(string occurrence, string key)
    {
        Assert.Equal(key, OccurrenceKeys.For(DateTimeOffset.Parse(occurrence, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void For_Occurrence_IsAWellFormedKeyInTheSchedulersReservedPrefix()
    {
        var key = OccurrenceKeys.For(new DateTimeOffset(2026, 11, 1, 19, 0, 0, TimeSpan.Zero));

        Assert.True(JobKeys.IsWellFormed(key));
        Assert.StartsWith(JobKeys.ReservedPrefix, key, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => JobKeys.EnsureEnqueueable(key));
    }
}
