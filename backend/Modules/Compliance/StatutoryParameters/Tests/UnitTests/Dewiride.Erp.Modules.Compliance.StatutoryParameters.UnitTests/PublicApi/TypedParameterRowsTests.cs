using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
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

    private static readonly ParameterSet PeriodSet = TestSets.Set(
        TestSets.Column("period", CellKind.Code, isKey: true),
        TestSets.Column("starts", CellKind.DayMonth),
        TestSets.Column("ends", CellKind.DayMonth, isOptional: true));

    private static readonly ParameterSet WindowSet = TestSets.Set(
        TestSets.Column("option", CellKind.Code, isKey: true),
        TestSets.Column("setUpOnOrAfter", CellKind.Date),
        TestSets.Column("productionBy", CellKind.Date, isOptional: true));

    [Fact]
    public void Read_DateCells_AsDatesAndAnEmptyOneAsNull()
    {
        var rows = new TypedParameterRows(WindowSet, [
            Row(WindowSet, ("option", "probe-a"), ("setUpOnOrAfter", "2019-10-01"), ("productionBy", "2024-03-31")),
            Row(WindowSet, ("option", "probe-b"), ("setUpOnOrAfter", "2016-03-01"), ("productionBy", null))]);

        Assert.Equal(
            [new ProbeWindow("probe-a", new DateOnly(2019, 10, 1), new DateOnly(2024, 3, 31)), new ProbeWindow("probe-b", new DateOnly(2016, 3, 1), null)],
            rows.Read<ProbeWindow>());
    }

    [Fact]
    public void Read_DayMonthCells_AsDaysAndMonthsAndAnEmptyOneAsNull()
    {
        var rows = new TypedParameterRows(PeriodSet, [
            Row(PeriodSet, ("period", "april-september"), ("starts", "04-01"), ("ends", "09-30")),
            Row(PeriodSet, ("period", "probe"), ("starts", "02-29"), ("ends", null))]);

        Assert.Equal(
            [new ProbePeriod("april-september", new DayAndMonth(4, 1), new DayAndMonth(9, 30)), new ProbePeriod("probe", new DayAndMonth(2, 29), null)],
            rows.Read<ProbePeriod>());
    }

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

    [Theory]
    [InlineData("5000000")]
    [InlineData("0.75")]
    public void Read_AmountCells_AsMoneyInRupeesWithTheirPaise(string amount)
    {
        var rows = new TypedParameterRows(ThresholdSet, [Row(ThresholdSet, ("threshold", "probe"), ("name", "Probe limit"), ("amount", amount), ("test", "Probe test."))]);

        Assert.Equal(new Money(decimal.Parse(amount, CultureInfo.InvariantCulture), Currency.Inr), Assert.Single(rows.Read<GstReturnThreshold>()).Amount);
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

    private sealed record ProbeWindow(string Option, DateOnly SetUpOnOrAfter, DateOnly? ProductionBy);

    private sealed record ProbePeriod(string Period, DayAndMonth Starts, DayAndMonth? Ends);
}
