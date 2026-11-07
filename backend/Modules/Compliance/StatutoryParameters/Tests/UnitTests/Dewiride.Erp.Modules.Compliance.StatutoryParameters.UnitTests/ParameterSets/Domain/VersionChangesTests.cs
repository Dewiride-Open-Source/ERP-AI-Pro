using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Timeline;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.ParameterSets.Domain;

public sealed class VersionChangesTests
{
    private static readonly ParameterSet Set = TestSets.RateSet();

    private static readonly ParameterRow Kept = TestSets.Rate(Set, "kept", "2017-07-01", null);

    private static readonly ParameterRow ChangedBefore = TestSets.Rate(Set, "changed", "2017-07-01", "2019-09-30");

    private static readonly ParameterRow ChangedAfter = TestSets.Rate(Set, "changed", "2019-10-01", null, "18");

    private static readonly ParameterRow Removed = TestSets.Rate(Set, "removed", "2017-07-01", "2019-09-30");

    private static readonly ParameterRow Added = TestSets.Rate(Set, "added", "2019-10-01", null);

    private static readonly ParameterRow Stopped = TestSets.Rate(Set, "returns", "2017-07-01", "2018-12-31");

    private static readonly ParameterRow Returned = TestSets.Rate(Set, "returns", "2019-10-01", null, "5");

    private static readonly ParameterTimeline Timeline = new(
        [TestSets.Day("2017-07-01"), TestSets.Day("2019-01-01"), TestSets.Day("2019-10-01")],
        [Kept, ChangedBefore, ChangedAfter, Removed, Added, Stopped, Returned]);

    [Fact]
    public void ChangesAt_FirstVersion_AddsEveryRowStartingOnIt()
    {
        var changes = Timeline.ChangesAt(TestSets.Day("2017-07-01"));

        Assert.Equal(["changed", "kept", "removed", "returns"], changes.Added.Select(row => row.RowKey));
        Assert.Empty(changes.Changed);
        Assert.Empty(changes.Removed);
    }

    [Fact]
    public void ChangesAt_LaterVersion_AddsChangesAndRemovesByKeyAndLeavesTheRestOut()
    {
        var changes = Timeline.ChangesAt(TestSets.Day("2019-10-01"));

        Assert.Equal([Added, Returned], changes.Added);
        Assert.Equal(new RowChange(ChangedBefore, ChangedAfter), Assert.Single(changes.Changed));
        Assert.Equal(Removed, Assert.Single(changes.Removed));
        Assert.False(changes.IsEmpty);
    }

    [Fact]
    public void ChangesAt_VersionWhereAKeyStops_RemovesIt()
    {
        var changes = Timeline.ChangesAt(TestSets.Day("2019-01-01"));

        Assert.Empty(changes.Added);
        Assert.Empty(changes.Changed);
        Assert.Equal(Stopped, Assert.Single(changes.Removed));
    }

    [Fact]
    public void ChangesAt_DayThatIsNoVersion_IsEmpty()
    {
        Assert.True(Timeline.ChangesAt(TestSets.Day("2018-01-01")).IsEmpty);
    }
}
