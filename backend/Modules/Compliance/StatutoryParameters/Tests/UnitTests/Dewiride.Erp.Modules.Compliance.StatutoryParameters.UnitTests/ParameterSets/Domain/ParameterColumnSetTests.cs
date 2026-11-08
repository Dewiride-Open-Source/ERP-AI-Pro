using System.Globalization;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.ParameterSets.Domain;

public sealed class ParameterColumnSetTests
{
    private static readonly ParameterChoice[] Periods = [new("month", "Month"), new("firstTwoMonthsOfQuarter", "First two months of a quarter")];

    [Theory]
    [InlineData("entry")]
    [InlineData("stateCode")]
    [InlineData("a")]
    [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMN")]
    public void Column_NameInCamelCaseOfAtMostFortyCharacters_IsDeclared(string name)
    {
        Assert.Equal(name, TestSets.Column(name, CellKind.Text).Name);
    }

    [Theory]
    [InlineData("Entry")]
    [InlineData("1entry")]
    [InlineData("state-code")]
    [InlineData("state code")]
    [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNO")]
    [InlineData("entry\n")]
    [InlineData("")]
    public void Column_NameNotInCamelCaseOrTooLong_IsRefused(string name)
    {
        Assert.Throws<ArgumentException>(() => TestSets.Column(name, CellKind.Text));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" Rate")]
    [InlineData("Rate ")]
    [InlineData("Rate  of tax")]
    [InlineData("Rate\tof tax")]
    [InlineData("Rate\u007f")]
    public void Column_TitleOutOfShape_IsRefused(string title)
    {
        Assert.Throws<ArgumentException>(() => new ParameterColumn("rate", title, CellKind.Percent, false, false, false, null, null, []));
    }

    [Fact]
    public void Column_TitleOfSixtyOneCharacters_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => new ParameterColumn("rate", new string('a', ParameterColumn.TitleMaxLength + 1), CellKind.Percent, false, false, false, null, null, []));
    }

    [Fact]
    public void Column_TitleOfSixtyCharacters_IsDeclared()
    {
        var title = new string('a', ParameterColumn.TitleMaxLength);

        Assert.Equal(title, new ParameterColumn("rate", title, CellKind.Percent, false, false, false, null, null, []).Title);
    }

    [Theory]
    [InlineData(nameof(CellKind.Code))]
    [InlineData(nameof(CellKind.StateCode))]
    public void Column_KeyOfACodeKind_IsDeclared(string kindName)
    {
        var kind = Enum.Parse<CellKind>(kindName);

        Assert.True(TestSets.Column("key", kind, isKey: true).IsKey);
    }

    [Theory]
    [InlineData(nameof(CellKind.Text))]
    [InlineData(nameof(CellKind.Integer))]
    [InlineData(nameof(CellKind.Percent))]
    [InlineData(nameof(CellKind.Amount))]
    [InlineData(nameof(CellKind.Date))]
    [InlineData(nameof(CellKind.DayMonth))]
    public void Column_KeyOfAnotherKind_IsRefused(string kindName)
    {
        var kind = Enum.Parse<CellKind>(kindName);

        Assert.Throws<ArgumentException>(() => TestSets.Column("key", kind, isKey: true));
    }

    [Fact]
    public void Column_OptionalKey_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => TestSets.Column("key", CellKind.Code, isKey: true, isOptional: true));
    }

    [Theory]
    [InlineData(nameof(CellKind.Integer), "1", "31")]
    [InlineData(nameof(CellKind.Percent), "0", "100")]
    [InlineData(nameof(CellKind.Percent), "0.0001", "99.9999")]
    [InlineData(nameof(CellKind.Amount), "0", "50000000")]
    [InlineData(nameof(CellKind.Amount), "0.75", "2.25")]
    public void Column_BoundsThatFitTheKind_AreDeclared(string kindName, string minimum, string maximum)
    {
        var kind = Enum.Parse<CellKind>(kindName);

        var column = TestSets.Column("value", kind, minimum: decimal.Parse(minimum, CultureInfo.InvariantCulture), maximum: decimal.Parse(maximum, CultureInfo.InvariantCulture));

        Assert.Equal((minimum, maximum), (CanonicalNumber.Of(column.Minimum!.Value), CanonicalNumber.Of(column.Maximum!.Value)));
    }

    [Theory]
    [InlineData(nameof(CellKind.Text), "1", null)]
    [InlineData(nameof(CellKind.Code), null, "1")]
    [InlineData(nameof(CellKind.Integer), "1.5", null)]
    [InlineData(nameof(CellKind.Percent), null, "100.5")]
    [InlineData(nameof(CellKind.Percent), "-1", null)]
    [InlineData(nameof(CellKind.Percent), "0.00001", null)]
    [InlineData(nameof(CellKind.Amount), "0.005", null)]
    [InlineData(nameof(CellKind.Amount), "-1", null)]
    [InlineData(nameof(CellKind.Integer), "31", "1")]
    [InlineData(nameof(CellKind.Date), "1", null)]
    [InlineData(nameof(CellKind.DayMonth), null, "1")]
    public void Column_BoundsThatDoNotFit_AreRefused(string kindName, string? minimum, string? maximum)
    {
        var kind = Enum.Parse<CellKind>(kindName);

        Assert.Throws<ArgumentException>(() => TestSets.Column("value", kind, minimum: Parse(minimum), maximum: Parse(maximum)));
    }

    [Fact]
    public void Column_ChoiceWithTwoCamelCaseValues_IsDeclared()
    {
        Assert.Equal(Periods, TestSets.Column("period", CellKind.Choice, choices: Periods).Choices);
    }

    [Fact]
    public void Column_ChoiceWithOneValue_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => TestSets.Column("period", CellKind.Choice, choices: [new("month", "Month")]));
    }

    [Theory]
    [InlineData("Month")]
    [InlineData("first-two")]
    [InlineData("month")]
    public void Column_ChoiceValueNotCamelCaseOrRepeated_IsRefused(string value)
    {
        Assert.Throws<ArgumentException>(() => TestSets.Column("period", CellKind.Choice, choices: [new("month", "Month"), new(value, "Another")]));
    }

    [Fact]
    public void Column_ChoiceTitleOutOfShape_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => TestSets.Column("period", CellKind.Choice, choices: [new("month", "Month"), new("quarter", " Quarter")]));
    }

    [Fact]
    public void Column_ChoicesOfAColumnThatIsNoChoice_AreRefused()
    {
        Assert.Throws<ArgumentException>(() => TestSets.Column("period", CellKind.Text, choices: Periods));
    }

    [Fact]
    public void Create_WithoutAColumn_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => ParameterColumnSet.Create([]));
    }

    [Fact]
    public void Create_TwoColumnsOfOneName_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => ParameterColumnSet.Create([TestSets.Column("entry", CellKind.Code, isKey: true), TestSets.Column("entry", CellKind.Text)]));
    }

    [Fact]
    public void Create_WithoutAKey_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => ParameterColumnSet.Create([TestSets.Column("entry", CellKind.Code)]));
    }

    [Fact]
    public void Create_TwoKeys_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => ParameterColumnSet.Create([TestSets.Column("entry", CellKind.Code, isKey: true), TestSets.Column("code", CellKind.Code, isKey: true)]));
    }

    [Fact]
    public void Create_TwoRowTitles_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => ParameterColumnSet.Create([
            TestSets.Column("entry", CellKind.Code, isKey: true),
            TestSets.Column("name", CellKind.Text, isRowTitle: true),
            TestSets.Column("title", CellKind.Text, isRowTitle: true)]));
    }

    [Fact]
    public void Create_OneKeyAndOneRowTitle_KeepsTheColumnsInTheirOrder()
    {
        var columns = ParameterColumnSet.Create([TestSets.Column("entry", CellKind.Code, isKey: true), TestSets.Column("name", CellKind.Text, isRowTitle: true)]);

        Assert.Equal("entry", columns.KeyColumn.Name);
        Assert.Equal(["entry", "name"], columns.Columns.Select(column => column.Name));
    }

    [Theory]
    [InlineData(nameof(CellKind.Text), "Selling of space for advertisement in print media.")]
    [InlineData(nameof(CellKind.Code), "21(ii)")]
    [InlineData(nameof(CellKind.Code), "GSTR-3B")]
    [InlineData(nameof(CellKind.Code), "gst.sac-codes")]
    [InlineData(nameof(CellKind.StateCode), "27")]
    [InlineData(nameof(CellKind.Integer), "11")]
    [InlineData(nameof(CellKind.Integer), "-3")]
    [InlineData(nameof(CellKind.Percent), "2.5")]
    [InlineData(nameof(CellKind.Percent), "0.0001")]
    [InlineData(nameof(CellKind.Percent), "100")]
    [InlineData(nameof(CellKind.Amount), "50000000")]
    [InlineData(nameof(CellKind.Amount), "0")]
    [InlineData(nameof(CellKind.Amount), "0.75")]
    [InlineData(nameof(CellKind.Amount), "1.5")]
    [InlineData(nameof(CellKind.Date), "2016-03-01")]
    [InlineData(nameof(CellKind.Date), "2024-02-29")]
    [InlineData(nameof(CellKind.DayMonth), "04-01")]
    [InlineData(nameof(CellKind.DayMonth), "12-31")]
    [InlineData(nameof(CellKind.DayMonth), "02-29")]
    public void Problem_ValueOfItsKindInCanonicalText_IsNone(string kindName, string value)
    {
        var kind = Enum.Parse<CellKind>(kindName);

        Assert.Null(TestSets.Column("value", kind).Problem(value));
    }

    [Theory]
    [InlineData(nameof(CellKind.Text), " leading")]
    [InlineData(nameof(CellKind.Text), "two  spaces")]
    [InlineData(nameof(CellKind.Text), "line\nfeed")]
    [InlineData(nameof(CellKind.Code), "21 (ii)")]
    [InlineData(nameof(CellKind.Code), "21(ii)\n")]
    [InlineData(nameof(CellKind.Code), "")]
    [InlineData(nameof(CellKind.StateCode), "7")]
    [InlineData(nameof(CellKind.StateCode), "277")]
    [InlineData(nameof(CellKind.StateCode), "٢٧")]
    [InlineData(nameof(CellKind.Integer), "11.5")]
    [InlineData(nameof(CellKind.Integer), "011")]
    [InlineData(nameof(CellKind.Integer), "1e1")]
    [InlineData(nameof(CellKind.Integer), "2147483648")]
    [InlineData(nameof(CellKind.Percent), "2.50")]
    [InlineData(nameof(CellKind.Percent), "100.0001")]
    [InlineData(nameof(CellKind.Percent), "0.12345")]
    [InlineData(nameof(CellKind.Percent), "-0.5")]
    [InlineData(nameof(CellKind.Amount), "10.505")]
    [InlineData(nameof(CellKind.Amount), "2.50")]
    [InlineData(nameof(CellKind.Amount), "-1")]
    [InlineData(nameof(CellKind.Amount), "-0.75")]
    [InlineData(nameof(CellKind.Amount), "5,00,00,000")]
    [InlineData(nameof(CellKind.Date), "2016-3-1")]
    [InlineData(nameof(CellKind.Date), "2016-02-30")]
    [InlineData(nameof(CellKind.Date), "2025-02-29")]
    [InlineData(nameof(CellKind.Date), "0000-01-01")]
    [InlineData(nameof(CellKind.Date), "01-03-2016")]
    [InlineData(nameof(CellKind.Date), "2016-03-01T00:00:00")]
    [InlineData(nameof(CellKind.Date), "1 March 2016")]
    [InlineData(nameof(CellKind.DayMonth), "4-1")]
    [InlineData(nameof(CellKind.DayMonth), "04-1")]
    [InlineData(nameof(CellKind.DayMonth), "13-01")]
    [InlineData(nameof(CellKind.DayMonth), "00-10")]
    [InlineData(nameof(CellKind.DayMonth), "02-30")]
    [InlineData(nameof(CellKind.DayMonth), "04-31")]
    [InlineData(nameof(CellKind.DayMonth), "2026-04-01")]
    [InlineData(nameof(CellKind.DayMonth), "04-01 ")]
    [InlineData(nameof(CellKind.DayMonth), "1 April")]
    public void Problem_ValueThatBreaksItsKind_IsNamed(string kindName, string value)
    {
        var kind = Enum.Parse<CellKind>(kindName);

        Assert.NotNull(TestSets.Column("value", kind).Problem(value));
    }

    [Fact]
    public void Problem_ValueOutsideTheColumnsBounds_IsNamed()
    {
        var column = TestSets.Column("dueDay", CellKind.Integer, minimum: 1, maximum: 31);

        Assert.Null(column.Problem("1"));
        Assert.Null(column.Problem("31"));
        Assert.NotNull(column.Problem("0"));
        Assert.NotNull(column.Problem("32"));
    }

    [Fact]
    public void Problem_ChoiceTheColumnDoesNotDeclare_IsNamed()
    {
        var column = TestSets.Column("period", CellKind.Choice, choices: Periods);

        Assert.Null(column.Problem("firstTwoMonthsOfQuarter"));
        Assert.NotNull(column.Problem("FirstTwoMonthsOfQuarter"));
        Assert.NotNull(column.Problem("quarter"));
    }

    [Fact]
    public void Problem_Null_IsNoneOnlyForAnOptionalColumn()
    {
        Assert.Null(TestSets.Column("note", CellKind.Text, isOptional: true).Problem(null));
        Assert.NotNull(TestSets.Column("note", CellKind.Text).Problem(null));
    }

    [Fact]
    public void ProblemsOf_CellsMissingOrUndeclared_NameEachColumn()
    {
        var columns = TestSets.RateSet().Columns;

        var problems = columns.ProblemsOf(TestSets.Cells(("entry", "21(ii)"), ("rates", "9"), ("note", null)));

        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, problem => problem.Contains("rate", StringComparison.Ordinal) && problem.Contains("no cell", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("rates", StringComparison.Ordinal) && problem.Contains("does not declare", StringComparison.Ordinal));
    }

    [Fact]
    public void Equals_ColumnsDeclaredAlike_IsTrueAndDifferingInATitle_IsFalse()
    {
        var first = ParameterColumnSet.Create([TestSets.Column("entry", CellKind.Code, isKey: true), TestSets.Column("period", CellKind.Choice, choices: Periods)]);
        var same = ParameterColumnSet.Create([TestSets.Column("entry", CellKind.Code, isKey: true), TestSets.Column("period", CellKind.Choice, choices: [.. Periods])]);
        var retitled = ParameterColumnSet.Create([TestSets.Column("entry", CellKind.Code, isKey: true), new ParameterColumn("period", "Tax period", CellKind.Choice, false, false, false, null, null, Periods)]);

        Assert.Equal(first, same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, retitled);
    }

    private static decimal? Parse(string? value) => value is null ? null : decimal.Parse(value, CultureInfo.InvariantCulture);
}
