using Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints.Export;

namespace Dewiride.Erp.Modules.Platform.Audit.UnitTests.Changes.Endpoints.Export;

public sealed class CsvCellsTests
{
    [Theory]
    [InlineData(null, "\"\"")]
    [InlineData("", "\"\"")]
    [InlineData("Payroll", "\"Payroll\"")]
    [InlineData("Say \"hello\"", "\"Say \"\"hello\"\"\"")]
    [InlineData("Finance, Tax", "\"Finance, Tax\"")]
    [InlineData("first\r\nsecond", "\"first\r\nsecond\"")]
    [InlineData("a=b", "\"a=b\"")]
    [InlineData(" =1+2", "\" =1+2\"")]
    [InlineData("'quoted'", "\"'quoted'\"")]
    public void Of_TextThatBeginsNoFormula_EnclosesItInQuotesAndDoublesTheQuotesInside(string? value, string cell)
    {
        Assert.Equal(cell, CsvCells.Of(value));
    }

    [Theory]
    [InlineData("=1+2")]
    [InlineData("+919876543210")]
    [InlineData("-5")]
    [InlineData("@SUM(A1:A2)")]
    [InlineData("\tindented")]
    [InlineData("\rreturn")]
    [InlineData("\nline")]
    [InlineData("＝1+2")]
    [InlineData("＋919876543210")]
    [InlineData("－5")]
    [InlineData("＠SUM(A1:A2)")]
    public void Of_TextThatWouldBeginAFormula_BeginsTheCellWithAnApostrophe(string value)
    {
        Assert.Equal($"\"'{value}\"", CsvCells.Of(value));
    }

    [Fact]
    public void Of_FormulaHoldingQuotes_BeginsWithAnApostropheAndDoublesTheQuotes()
    {
        Assert.Equal("\"'=HYPERLINK(\"\"https://example.com\"\")\"", CsvCells.Of("=HYPERLINK(\"https://example.com\")"));
    }
}
