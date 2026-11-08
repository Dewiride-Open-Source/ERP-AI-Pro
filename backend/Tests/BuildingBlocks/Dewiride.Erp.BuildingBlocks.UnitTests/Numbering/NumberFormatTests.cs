using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.BuildingBlocks.Numbering.Rules;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Numbering;

// The rule is made up, so that no statutory value is written in a test that is not the parameter set's golden test.
public sealed class NumberFormatTests
{
    private static readonly SerialNumberRule Rule = new(14, "-.");

    private static readonly FinancialYear Year = new(2026);

    [Theory]
    [InlineData("TI.", NumberYearStyle.Full, ".", 3, 1L, "TI.2026-27.001")]
    [InlineData("TI.", NumberYearStyle.Abbreviated, "-", 4, 25L, "TI.26-27-0025")]
    [InlineData("TI", NumberYearStyle.None, "", 5, 7L, "TI00007")]
    [InlineData("", NumberYearStyle.Full, ".", 1, 9L, "2026-27.9")]
    [InlineData("", NumberYearStyle.None, "", 1, 12345L, "12345")]
    [InlineData("A", NumberYearStyle.None, "", 2, 1234L, "A1234")]
    public void Render_ANumber_PutsThePrefixTheYearTheSeparatorAndTheRunningNumberPaddedToItsDigits(string prefix, NumberYearStyle year, string separator, int digits, long number, string expected)
    {
        Assert.Equal(expected, new NumberFormat(prefix, year, separator, digits).Render(Year, number));
    }

    [Fact]
    public void Render_TheFinancialYear202728_ShowsThatYear()
    {
        Assert.Equal("TI.27-28.01", new NumberFormat("TI.", NumberYearStyle.Abbreviated, ".", 2).Render(Year.Next, 1));
    }

    [Fact]
    public void Render_ARunningNumberBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NumberFormat("TI", NumberYearStyle.None, "", 1).Render(Year, 0));
    }

    [Theory]
    [InlineData("TI.", NumberYearStyle.Full, ".", 3, 14, 999L)]
    [InlineData("TI.", NumberYearStyle.Full, ".", 1, 14, 999L)]
    [InlineData("TI", NumberYearStyle.None, "", 1, 14, 999_999_999_999L)]
    [InlineData("TI.", NumberYearStyle.Full, ".", 4, 14, 0L)]
    [InlineData("TI", NumberYearStyle.None, "", 1, 40, 999_999_999_999_999_999L)]
    public void LastNumberWithin_ALength_IsTheLastRunningNumberWhoseNumberFits(string prefix, NumberYearStyle year, string separator, int digits, int maxLength, long expected)
    {
        Assert.Equal(expected, new NumberFormat(prefix, year, separator, digits).LastNumberWithin(maxLength));
    }

    [Theory]
    [InlineData("TI.", NumberYearStyle.Full, ".", 3)]
    [InlineData("T-I", NumberYearStyle.Abbreviated, "-", 2)]
    [InlineData("", NumberYearStyle.Full, ".", 2)]
    [InlineData("", NumberYearStyle.None, "", 1)]
    [InlineData("9", NumberYearStyle.None, "", 6)]
    public void ProblemsUnder_AFormatTheRuleAllows_HasNone(string prefix, NumberYearStyle year, string separator, int digits)
    {
        Assert.Empty(new NumberFormat(prefix, year, separator, digits).ProblemsUnder(Rule, Year));
    }

    [Theory]
    [InlineData("ti.", NumberYearStyle.Full, ".", 3, NumberFormat.PrefixField)]
    [InlineData("TI/", NumberYearStyle.Full, ".", 3, NumberFormat.PrefixField)]
    [InlineData("TI INV", NumberYearStyle.None, "", 3, NumberFormat.PrefixField)]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFG", NumberYearStyle.None, "", 1, NumberFormat.PrefixField)]
    [InlineData("TI", (NumberYearStyle)7, "", 3, NumberFormat.YearField)]
    [InlineData("TI", NumberYearStyle.Full, "/", 3, NumberFormat.SeparatorField)]
    [InlineData("TI", NumberYearStyle.Full, "..", 3, NumberFormat.SeparatorField)]
    [InlineData("TI", NumberYearStyle.None, ".", 3, NumberFormat.SeparatorField)]
    [InlineData("TI", NumberYearStyle.None, "", 0, NumberFormat.DigitsField)]
    [InlineData("TI", NumberYearStyle.None, "", 19, NumberFormat.DigitsField)]
    [InlineData("INVOICE.", NumberYearStyle.Full, ".", 3, NumberFormat.FormatField)]
    [InlineData("0A", NumberYearStyle.None, "", 3, NumberFormat.PrefixField)]
    [InlineData("-A", NumberYearStyle.None, "", 3, NumberFormat.PrefixField)]
    [InlineData("", NumberYearStyle.None, "", 3, NumberFormat.DigitsField)]
    public void ProblemsUnder_AFormatTheRuleRefuses_NamesTheFieldAtFault(string prefix, NumberYearStyle year, string separator, int digits, string field)
    {
        var problems = new NumberFormat(prefix, year, separator, digits).ProblemsUnder(Rule, Year);

        Assert.Equal([field], problems.Keys);
        Assert.NotEmpty(Assert.Single(problems.Values));
    }

    [Fact]
    public void ProblemsUnder_ARuleWithoutTheHyphen_RefusesShowingTheYear()
    {
        var rule = Rule with { SpecialCharacters = "." };

        var problems = new NumberFormat("TI.", NumberYearStyle.Full, ".", 3).ProblemsUnder(rule, Year);

        Assert.Equal("The financial year is written with - (hyphen), which a number may not contain, so leave the year out.", Assert.Single(Assert.Single(problems).Value));
        Assert.Equal(NumberFormat.YearField, Assert.Single(problems).Key);
        Assert.Empty(new NumberFormat("TI.", NumberYearStyle.None, "", 3).ProblemsUnder(rule, Year));
    }

    [Fact]
    public void ProblemsUnder_ARuleWithoutSpecialCharacters_AllowsOnlyLettersAndDigits()
    {
        var rule = Rule with { SpecialCharacters = string.Empty };

        var prefix = new NumberFormat("TI.", NumberYearStyle.None, "", 3).ProblemsUnder(rule, Year);
        var separator = new NumberFormat("TI", NumberYearStyle.Full, ".", 3).ProblemsUnder(rule, Year);

        Assert.Equal("Use only capital letters and digits.", Assert.Single(prefix[NumberFormat.PrefixField]));
        Assert.Equal("Leave it empty, because a number may contain only capital letters and digits.", Assert.Single(separator[NumberFormat.SeparatorField]));
        Assert.Empty(new NumberFormat("TI9", NumberYearStyle.None, "", 3).ProblemsUnder(rule, Year));
    }

    [Fact]
    public void ProblemsUnder_ACharacterTheRuleRefuses_NamesTheAllowedCharactersInWords()
    {
        var prefix = new NumberFormat("T/I", NumberYearStyle.None, "", 3).ProblemsUnder(Rule, Year);
        var separator = new NumberFormat("TI", NumberYearStyle.Full, "/", 3).ProblemsUnder(Rule, Year);
        var other = new NumberFormat("T/I", NumberYearStyle.None, "", 3).ProblemsUnder(Rule with { SpecialCharacters = "_+" }, Year);

        Assert.Equal("Use only capital letters, digits, - (hyphen) and . (full stop).", Assert.Single(prefix[NumberFormat.PrefixField]));
        Assert.Equal("Use - (hyphen) or . (full stop), or nothing.", Assert.Single(separator[NumberFormat.SeparatorField]));
        Assert.Equal("Use only capital letters, digits, _ (underscore) and \"+\".", Assert.Single(other[NumberFormat.PrefixField]));
    }

    [Fact]
    public void ProblemsUnder_APrefixLongerThanAPrefixMayBe_SaysHowLongItMayBe()
    {
        var problems = new NumberFormat(new string('T', NumberFormat.PrefixMaxLength + 1), NumberYearStyle.None, "", 1).ProblemsUnder(Rule, Year);

        Assert.Equal($"Use at most {NumberFormat.PrefixMaxLength} characters.", Assert.Single(problems[NumberFormat.PrefixField]));
    }

    [Fact]
    public void FixedLength_OfEachYearStyle_CountsThePrefixTheYearAndTheSeparator()
    {
        Assert.Equal(11, new NumberFormat("TI.", NumberYearStyle.Full, ".", 3).FixedLength);
        Assert.Equal(9, new NumberFormat("TI.", NumberYearStyle.Abbreviated, ".", 3).FixedLength);
        Assert.Equal(3, new NumberFormat("TI.", NumberYearStyle.None, "", 3).FixedLength);
    }
}
