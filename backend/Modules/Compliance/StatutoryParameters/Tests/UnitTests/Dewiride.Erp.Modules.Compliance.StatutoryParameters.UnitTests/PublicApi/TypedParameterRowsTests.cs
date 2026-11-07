using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Returns;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Services;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.PublicApi;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.ParameterSets;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.PublicApi;

public sealed class TypedParameterRowsTests
{
    private static readonly ParameterSet RateSet = TestSets.Set(
        TestSets.Column("entry", CellKind.Code, isKey: true),
        TestSets.Column("heading", CellKind.Text),
        TestSets.Column("description", CellKind.Text),
        TestSets.Column("rate", CellKind.Percent),
        TestSets.Column("condition", CellKind.Text, isOptional: true));

    private static readonly ParameterSet DueDateSet = TestSets.Set(
        TestSets.Column("rule", CellKind.Code, isKey: true),
        TestSets.Column("name", CellKind.Text, isRowTitle: true),
        TestSets.Column("form", CellKind.Code),
        TestSets.Column("filers", CellKind.Choice, choices: [new("monthly", "Monthly"), new("quarterly", "Quarterly"), new("all", "All")]),
        TestSets.Column("period", CellKind.Choice, choices: [new("month", "Month"), new("firstTwoMonthsOfQuarter", "First two months of a quarter")]),
        TestSets.Column("opensDay", CellKind.Integer, isOptional: true),
        TestSets.Column("dueDay", CellKind.Integer),
        TestSets.Column("monthsAfter", CellKind.Integer),
        TestSets.Column("wording", CellKind.Text));

    private static readonly ParameterSet ThresholdSet = TestSets.Set(
        TestSets.Column("threshold", CellKind.Code, isKey: true),
        TestSets.Column("name", CellKind.Text),
        TestSets.Column("amount", CellKind.Amount),
        TestSets.Column("test", CellKind.Text));

    [Fact]
    public void Read_PercentCells_AsPercentagesAndAnEmptyConditionAsNull()
    {
        var rows = new TypedParameterRows(RateSet, [Row(RateSet, ("entry", "a"), ("heading", "Probe heading"), ("description", "Probe description."), ("rate", "2.5"), ("condition", null))]);

        var entry = Assert.Single(rows.Read<ServiceRateEntry>());

        Assert.Equal(new ServiceRateEntry("a", "Probe heading", "Probe description.", new Percentage(2.5m), null), entry);
    }

    [Fact]
    public void Read_ChoiceAndIntegerCells_AsEnumMembersAndNumbersWithAnEmptyOneAsNull()
    {
        var rows = new TypedParameterRows(DueDateSet, [Row(
            DueDateSet,
            ("rule", "probe"),
            ("name", "Probe return"),
            ("form", "PROBE-1"),
            ("filers", "quarterly"),
            ("period", "firstTwoMonthsOfQuarter"),
            ("opensDay", null),
            ("dueDay", "25"),
            ("monthsAfter", "1"),
            ("wording", "Probe wording."))]);

        var rule = Assert.Single(rows.Read<GstReturnDueDate>());

        Assert.Equal(new GstReturnDueDate("probe", "Probe return", "PROBE-1", ReturnFilers.Quarterly, ReturnPeriod.FirstTwoMonthsOfQuarter, null, 25, 1, "Probe wording."), rule);
    }

    [Fact]
    public void Read_AmountCells_AsMoneyInRupees()
    {
        var rows = new TypedParameterRows(ThresholdSet, [Row(ThresholdSet, ("threshold", "probe"), ("name", "Probe limit"), ("amount", "5000000"), ("test", "Probe test."))]);

        Assert.Equal(new Money(5000000m, Currency.Inr), Assert.Single(rows.Read<GstReturnThreshold>()).Amount);
    }

    [Fact]
    public void Read_RowsOfTheVersion_KeepTheirOrder()
    {
        var rows = new TypedParameterRows(ThresholdSet, [
            Row(ThresholdSet, ("threshold", "b"), ("name", "Second"), ("amount", "2"), ("test", "Probe test.")),
            Row(ThresholdSet, ("threshold", "a"), ("name", "First"), ("amount", "1"), ("test", "Probe test."))]);

        Assert.Equal(["b", "a"], rows.Read<GstReturnThreshold>().Select(threshold => threshold.Threshold));
    }

    [Fact]
    public void Read_IntoARecordThatLacksAColumn_Throws()
    {
        var rows = new TypedParameterRows(RateSet, [Row(RateSet, ("entry", "a"), ("heading", "Probe heading"), ("description", "Probe description."), ("rate", "9"), ("condition", null))]);

        Assert.Throws<InvalidOperationException>(() => rows.Read<SacRateEntry>());
    }

    [Fact]
    public void Read_IntoARecordWithAParameterNoColumnFills_Throws()
    {
        var rows = new TypedParameterRows(ThresholdSet, [Row(ThresholdSet, ("threshold", "probe"), ("name", "Probe limit"), ("amount", "1"), ("test", "Probe test."))]);

        Assert.Throws<InvalidOperationException>(() => rows.Read<GstReturnDueDate>());
    }

    [Fact]
    public void Read_EmptyCellIntoAParameterThatIsNotNullable_Throws()
    {
        var set = TestSets.Set(
            TestSets.Column("code", CellKind.Code, isKey: true),
            TestSets.Column("entry", CellKind.Code, isOptional: true),
            TestSets.Column("basis", CellKind.Text));
        var rows = new TypedParameterRows(set, [Row(set, ("code", "a"), ("entry", null), ("basis", "Probe basis."))]);

        Assert.Throws<InvalidOperationException>(() => rows.Read<SacRateEntry>());
    }

    [Fact]
    public void Read_ChoiceTheEnumLacks_Throws()
    {
        var set = TestSets.Set(
            TestSets.Column("code", CellKind.Code, isKey: true),
            TestSets.Column("level", CellKind.Choice, choices: [new("heading", "Heading"), new("chapter", "Chapter")]),
            TestSets.Column("parent", CellKind.Code, isOptional: true),
            TestSets.Column("description", CellKind.Text));
        var rows = new TypedParameterRows(set, [Row(set, ("code", "a"), ("level", "chapter"), ("parent", null), ("description", "Probe description."))]);

        Assert.Throws<InvalidOperationException>(() => rows.Read<SacCode>());
    }

    private static ParameterRow Row(ParameterSet set, params (string Column, string? Value)[] cells) =>
        ParameterRow.Create(set, TestSets.Day("2017-07-01"), null, TestSets.Cells(cells), "Probe source");
}
