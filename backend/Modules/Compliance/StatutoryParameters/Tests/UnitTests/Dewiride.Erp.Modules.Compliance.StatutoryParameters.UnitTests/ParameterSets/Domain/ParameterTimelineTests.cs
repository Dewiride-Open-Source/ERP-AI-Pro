using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Timeline;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.ParameterSets.Domain;

public sealed class ParameterTimelineTests
{
    private static readonly ParameterSet Set = TestSets.RateSet();

    private static readonly string[] ThreeVersions = ["2017-07-01", "2019-01-01", "2019-10-01"];

    [Fact]
    public void Problems_RowsEndingTheDayBeforeALaterVersion_AreNone()
    {
        var timeline = Timeline(
            ThreeVersions,
            TestSets.Rate(Set, "a", "2017-07-01", "2018-12-31"),
            TestSets.Rate(Set, "a", "2019-01-01", "2019-09-30", "12"),
            TestSets.Rate(Set, "a", "2019-10-01", null, "18"));

        Assert.Empty(timeline.Problems());
    }

    [Fact]
    public void Problems_NoVersion_NamesIt()
    {
        Assert.Contains("no version", Assert.Single(Timeline([]).Problems()), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2019-01-01", "2017-07-01")]
    [InlineData("2017-07-01", "2017-07-01")]
    public void Problems_VersionsNotStrictlyIncreasing_NameTheLaterOne(string first, string second)
    {
        var timeline = Timeline([first, second], TestSets.Rate(Set, "a", "2017-07-01", null), TestSets.Rate(Set, "b", "2019-01-01", null));

        Assert.Contains(timeline.Problems(), problem => problem.Contains("not later than the version before it", StringComparison.Ordinal));
    }

    [Fact]
    public void Problems_RowStartingOffAVersionsDate_NamesIt()
    {
        var timeline = Timeline(ThreeVersions, TestSets.Rate(Set, "a", "2017-07-01", null), TestSets.Rate(Set, "b", "2017-07-02", null));

        Assert.Contains(timeline.Problems(), problem => problem.Contains("Row b starts on 2017-07-02, which is no version's date", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("2018-12-30")]
    [InlineData("2019-01-01")]
    [InlineData("2020-12-31")]
    public void Problems_RowEndingOffTheDayBeforeALaterVersion_NamesIt(string until)
    {
        var timeline = Timeline(ThreeVersions, TestSets.Rate(Set, "a", "2017-07-01", until), TestSets.Rate(Set, "b", "2019-01-01", null), TestSets.Rate(Set, "c", "2019-10-01", null));

        Assert.Contains(timeline.Problems(), problem => problem.Contains($"ends on {until}, which is not the day before a later version's date", StringComparison.Ordinal));
    }

    [Fact]
    public void Problems_RowsOfOneKeyOverlappingByOneDay_NameTheKey()
    {
        var timeline = Timeline(
            ["2017-07-01", "2019-10-01", "2019-10-02"],
            TestSets.Rate(Set, "a", "2017-07-01", "2019-10-01"),
            TestSets.Rate(Set, "a", "2019-10-01", null, "18"));

        Assert.Contains(timeline.Problems(), problem => problem.Contains("rows of key a from 2017-07-01 and from 2019-10-01 overlap", StringComparison.Ordinal));
    }

    [Fact]
    public void Problems_KeyThatStopsAndReturnsAfterAGap_AreNone()
    {
        var timeline = Timeline(
            ThreeVersions,
            TestSets.Rate(Set, "a", "2017-07-01", "2018-12-31"),
            TestSets.Rate(Set, "a", "2019-10-01", null),
            TestSets.Rate(Set, "b", "2017-07-01", null));

        Assert.Empty(timeline.Problems());
    }

    [Fact]
    public void Problems_VersionThatChangesNothing_NamesIt()
    {
        var timeline = Timeline(ThreeVersions, TestSets.Rate(Set, "a", "2017-07-01", "2018-12-31"), TestSets.Rate(Set, "a", "2019-01-01", null));

        Assert.Equal("The version from 2019-10-01 changes nothing: no row starts on it or ends the day before.", Assert.Single(timeline.Problems()));
    }

    [Fact]
    public void Problems_FirstVersionThatStartsNoRow_NamesIt()
    {
        var timeline = Timeline(["2017-07-01", "2019-01-01"], TestSets.Rate(Set, "a", "2019-01-01", null));

        Assert.Equal("The first version, from 2017-07-01, starts no row.", Assert.Single(timeline.Problems()));
    }

    [Fact]
    public void Problems_VersionInTheFuture_AreNone()
    {
        var timeline = Timeline(["2017-07-01", "2999-04-01"], TestSets.Rate(Set, "a", "2017-07-01", "2999-03-31"), TestSets.Rate(Set, "a", "2999-04-01", null, "40"));

        Assert.Empty(timeline.Problems());
    }

    [Theory]
    [InlineData("2017-06-30", null, null)]
    [InlineData("2017-07-01", "2017-07-01", "2018-12-31")]
    [InlineData("2018-12-31", "2017-07-01", "2018-12-31")]
    [InlineData("2019-01-01", "2019-01-01", "2019-09-30")]
    [InlineData("2019-10-01", "2019-10-01", null)]
    [InlineData("2099-01-01", "2019-10-01", null)]
    public void VersionOn_Day_IsTheLatestVersionFromOnOrBeforeIt(string day, string? version, string? until)
    {
        var timeline = Timeline(ThreeVersions);

        var found = timeline.VersionOn(TestSets.Day(day));

        Assert.Equal(version is null ? null : TestSets.Day(version), found);
        Assert.Equal(until is null ? null : TestSets.Day(until), found is { } from ? timeline.UntilOf(from) : null);
    }

    [Theory]
    [InlineData("2016-01-01", "2017-07-01")]
    [InlineData("2018-12-31", "2019-01-01")]
    [InlineData("2019-01-01", "2019-10-01")]
    [InlineData("2019-10-01", null)]
    public void NextAfter_Day_IsTheFirstVersionAfterIt(string day, string? next)
    {
        Assert.Equal(next is null ? null : TestSets.Day(next), Timeline(ThreeVersions).NextAfter(TestSets.Day(day)));
    }

    [Theory]
    [InlineData("2018-12-31", "9")]
    [InlineData("2019-01-01", "12")]
    [InlineData("2019-09-30", "12")]
    [InlineData("2019-10-01", "18")]
    public void RowsOn_DayAtTheEdgeOfARow_IsTheRowInForceThatDay(string day, string rate)
    {
        var timeline = Timeline(
            ThreeVersions,
            TestSets.Rate(Set, "a", "2017-07-01", "2018-12-31"),
            TestSets.Rate(Set, "a", "2019-01-01", "2019-09-30", "12"),
            TestSets.Rate(Set, "a", "2019-10-01", null, "18"));

        Assert.Equal(rate, Assert.Single(timeline.RowsOn(TestSets.Day(day))).Cells["rate"]);
    }

    [Fact]
    public void RowsOn_Day_ListsTheRowsByKey()
    {
        var timeline = Timeline(["2017-07-01"], TestSets.Rate(Set, "b", "2017-07-01", null), TestSets.Rate(Set, "a", "2017-07-01", null), TestSets.Rate(Set, "B", "2017-07-01", null));

        Assert.Equal(["B", "a", "b"], timeline.RowsOn(TestSets.Day("2017-07-01")).Select(row => row.RowKey));
    }

    private static ParameterTimeline Timeline(string[] versions, params ParameterRow[] rows) =>
        new(versions.Select(TestSets.Day), rows);
}
