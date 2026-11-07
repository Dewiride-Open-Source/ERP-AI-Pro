using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Timeline;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.ParameterSets.Seeding;

public sealed class SeedCoverageTests
{
    private static readonly IReadOnlyList<IndianStateEntry> NoStates = [];

    [Theory]
    [InlineData("2017-07-01", null)]
    [InlineData("2017-07-01", "2018-12-31")]
    [InlineData("2018-12-31", "2019-01-01")]
    [InlineData("2019-01-01", null)]
    public void Problems_ReferenceCoveredByTheUnionOfTheReferencedRows_AreNone(string from, string? until)
    {
        var problems = SeedCoverage.Problems([Referenced(("x", "2017-07-01", "2018-12-31"), ("x", "2019-01-01", null)), Referencing("x", from, until)], NoStates);

        Assert.Empty(problems);
    }

    [Theory]
    [InlineData("2017-06-30", "2018-12-31")]
    [InlineData("2017-07-01", null)]
    [InlineData("2018-12-31", "2019-01-01")]
    public void Problems_ReferenceUncoveredByOneDayAtEitherEnd_NameTheRow(string from, string? until)
    {
        var problems = SeedCoverage.Problems([Referenced(("x", "2017-07-01", "2018-12-31")), Referencing("x", from, until)], NoStates);

        Assert.Contains("'x' is not a row key of test.referenced on every day", Assert.Single(problems), StringComparison.Ordinal);
    }

    [Fact]
    public void Problems_ReferenceAcrossAGapBetweenTheReferencedRows_NameTheRow()
    {
        var problems = SeedCoverage.Problems([Referenced(("x", "2017-07-01", "2018-12-30"), ("x", "2019-01-01", null)), Referencing("x", "2017-07-01", null)], NoStates);

        Assert.Single(problems);
    }

    [Fact]
    public void Problems_ReferenceToAnotherKey_NameTheRow()
    {
        var problems = SeedCoverage.Problems([Referenced(("x", "2017-07-01", null)), Referencing("y", "2017-07-01", null)], NoStates);

        Assert.Single(problems);
    }

    [Fact]
    public void Problems_ReferenceOfAnEmptyOptionalCell_AreNone()
    {
        var problems = SeedCoverage.Problems([Referenced(("x", "2017-07-01", null)), Referencing(null, "2017-07-01", null)], NoStates);

        Assert.Empty(problems);
    }

    [Fact]
    public void Problems_ReferenceToASetNoFileHolds_NameTheColumn()
    {
        var problems = SeedCoverage.Problems([Referencing("x", "2017-07-01", null)], NoStates);

        Assert.Contains("column code references test.referenced, which no seed file holds", Assert.Single(problems), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2017-07-01", "2020-07-31", true)]
    [InlineData("2017-07-01", "2020-08-01", false)]
    [InlineData("2017-06-30", "2020-07-31", false)]
    [InlineData("2020-08-01", null, false)]
    public void Problems_StateCodeInForceOnEveryDayOfTheRow_AreNoneAndOtherwiseNameTheRow(string from, string? until, bool covered)
    {
        var set = TestSets.Set(TestSets.Column("stateCode", CellKind.StateCode, isKey: true));
        var row = ParameterRow.Create(set, TestSets.Day(from), until is null ? null : TestSets.Day(until), TestSets.Cells(("stateCode", "25")), "Probe source");
        IReadOnlyList<IndianStateEntry> states = [new("25", "Probe territory", IndianStateKind.UnionTerritory, TestSets.Day("2017-07-01"), TestSets.Day("2020-07-31"))];

        var problems = SeedCoverage.Problems([new SeededSet(set, [], [row], new Dictionary<string, IReadOnlyList<string>>())], states);

        Assert.Equal(covered, problems.Count == 0);
    }

    [Fact]
    public void Problems_StateCodeOverTwoRowsOfTheReferenceData_AreNone()
    {
        var set = TestSets.Set(TestSets.Column("stateCode", CellKind.StateCode, isKey: true));
        var row = ParameterRow.Create(set, TestSets.Day("2017-07-01"), null, TestSets.Cells(("stateCode", "26")), "Probe source");
        IReadOnlyList<IndianStateEntry> states =
        [
            new("26", "Probe territory", IndianStateKind.UnionTerritory, TestSets.Day("2017-07-01"), TestSets.Day("2020-01-25")),
            new("26", "Merged probe territory", IndianStateKind.UnionTerritory, TestSets.Day("2020-01-26"), null),
        ];

        Assert.Empty(SeedCoverage.Problems([new SeededSet(set, [], [row], new Dictionary<string, IReadOnlyList<string>>())], states));
    }

    [Fact]
    public void IsCovered_SpanOpenAtTheEnd_NeedsASpanOpenAtTheEnd()
    {
        var span = new DateSpan(TestSets.Day("2017-07-01"), null);

        Assert.False(DateSpan.IsCovered(span, [new DateSpan(TestSets.Day("2017-07-01"), TestSets.Day("2999-12-31"))]));
        Assert.True(DateSpan.IsCovered(span, [new DateSpan(TestSets.Day("2017-07-01"), TestSets.Day("2018-12-31")), new DateSpan(TestSets.Day("2019-01-01"), null)]));
    }

    private static SeededSet Referenced(params (string Key, string From, string? Until)[] rows)
    {
        var set = ParameterSet.Create("test.referenced", "Referenced", "A set the tests build.", SetBasis.Law, AsOfDate.SupplyDate, ParameterColumnSet.Create([TestSets.Column("code", CellKind.Code, isKey: true)]));

        return new SeededSet(
            set,
            [],
            [.. rows.Select(row => ParameterRow.Create(set, TestSets.Day(row.From), row.Until is null ? null : TestSets.Day(row.Until), TestSets.Cells(("code", row.Key)), "Probe source"))],
            new Dictionary<string, IReadOnlyList<string>>());
    }

    private static SeededSet Referencing(string? code, string from, string? until)
    {
        var set = ParameterSet.Create("test.referencing", "Referencing", "A set the tests build.", SetBasis.Law, AsOfDate.SupplyDate, ParameterColumnSet.Create([
            TestSets.Column("entry", CellKind.Code, isKey: true),
            TestSets.Column("code", CellKind.Code, isOptional: true)]));
        var row = ParameterRow.Create(set, TestSets.Day(from), until is null ? null : TestSets.Day(until), TestSets.Cells(("entry", "e"), ("code", code)), "Probe source");

        return new SeededSet(set, [], [row], new Dictionary<string, IReadOnlyList<string>> { ["code"] = ["test.referenced"] });
    }
}
