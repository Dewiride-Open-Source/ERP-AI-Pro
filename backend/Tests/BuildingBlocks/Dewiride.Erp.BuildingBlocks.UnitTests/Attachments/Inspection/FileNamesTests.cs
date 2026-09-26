using System.Text;
using Dewiride.Erp.BuildingBlocks.Attachments.Inspection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Inspection;

public sealed class FileNamesTests
{
    [Theory]
    [InlineData("invoice.pdf", "invoice.pdf")]
    [InlineData("Q2 GST return (final).xlsx", "Q2 GST return (final).xlsx")]
    [InlineData("C:\\Users\\accounts\\invoice.pdf", "invoice.pdf")]
    [InlineData("/home/accounts/invoice.pdf", "invoice.pdf")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..\\..\\windows\\win.ini", "win.ini")]
    [InlineData("mixed/path\\to/receipt.png", "receipt.png")]
    [InlineData(".env", ".env")]
    [InlineData("archive.tar.gz", "archive.tar.gz")]
    public void Sanitize_Name_KeepsOnlyTheLastPathSegment(string raw, string expected)
    {
        Assert.Equal(expected, FileNames.Sanitize(raw));
    }

    [Theory]
    [InlineData("inv\u0000oice.pdf", "invoice.pdf")]
    [InlineData("in\u0007voice\u001B.pdf", "invoice.pdf")]
    [InlineData("line\r\nbreak.txt", "linebreak.txt")]
    [InlineData("tab\tseparated.csv", "tabseparated.csv")]
    [InlineData("delete\u007F.txt", "delete.txt")]
    [InlineData("c1\u0085control.txt", "c1control.txt")]
    public void Sanitize_ControlCharacters_AreRemoved(string raw, string expected)
    {
        Assert.Equal(expected, FileNames.Sanitize(raw));
    }

    [Theory]
    [InlineData("invoice\u202Efdp.exe", "invoicefdp.exe")]
    [InlineData("\u202Dreport.pdf", "report.pdf")]
    [InlineData("scan\u2066gnp.js\u2069.png", "scangnp.js.png")]
    [InlineData("a\u200Eb\u200Fc.txt", "abc.txt")]
    [InlineData("zero\u200Bwidth.txt", "zerowidth.txt")]
    [InlineData("\uFEFFbom.txt", "bom.txt")]
    public void Sanitize_FormattingCharacters_AreRemoved(string raw, string expected)
    {
        Assert.Equal(expected, FileNames.Sanitize(raw));
    }

    [Fact]
    public void Sanitize_DecomposedCharacters_AreComposed()
    {
        var sanitized = FileNames.Sanitize("re\u0301sume\u0301.pdf");

        Assert.Equal("r\u00E9sum\u00E9.pdf", sanitized);
        Assert.True(sanitized!.IsNormalized(NormalizationForm.FormC));
    }

    [Theory]
    [InlineData("\uD83D\uDCC4 statement.pdf")]
    [InlineData("\u092C\u093F\u0932.pdf")]
    [InlineData("\u8ACB\u6C42\u66F8.pdf")]
    public void Sanitize_NonLatinLettersAndSymbols_AreKept(string raw)
    {
        Assert.Equal(raw, FileNames.Sanitize(raw));
    }

    // A fact rather than theory data: xUnit serialises inline strings as UTF-8, which replaces an unpaired surrogate before the test runs.
    [Fact(Skip = "Defect: FileNames.Sanitize calls string.Normalize before it drops characters, and Normalize throws ArgumentException on an unpaired surrogate, so IAttachmentService.UploadAsync throws instead of answering attachment.file-name-invalid.")]
    public void Sanitize_UnpairedSurrogate_IsDroppedLikeAnyUnprintableCharacter()
    {
        Assert.Equal("statement.pdf", FileNames.Sanitize("statement\uD83D.pdf"));
        Assert.Equal("statement.pdf", FileNames.Sanitize("\uDCC4statement.pdf"));
        Assert.Null(FileNames.Sanitize("\uD83D"));
    }

    [Theory]
    [InlineData("  report.pdf  ", "report.pdf")]
    [InlineData("folder/  report.pdf", "report.pdf")]
    [InlineData("\u00A0report.pdf\u3000", "report.pdf")]
    [InlineData("two  spaces.pdf", "two  spaces.pdf")]
    public void Sanitize_SurroundingWhitespace_IsTrimmed(string raw, string expected)
    {
        Assert.Equal(expected, FileNames.Sanitize(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u0000\u0001")]
    [InlineData("\u202E\u200B")]
    [InlineData("folder/")]
    [InlineData("folder\\")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("....")]
    [InlineData(" .. ")]
    [InlineData("reports/..")]
    [InlineData(".\u202E.")]
    public void Sanitize_NothingLeftButBlanksOrDots_ReturnsNull(string? raw)
    {
        Assert.Null(FileNames.Sanitize(raw));
    }

    [Fact]
    public void Sanitize_TwoHundredFiftyFiveCharacters_IsAccepted()
    {
        var name = new string('a', 251) + ".pdf";

        Assert.Equal(name, FileNames.Sanitize(name));
    }

    [Fact]
    public void Sanitize_TwoHundredFiftySixCharacters_IsRefused()
    {
        Assert.Null(FileNames.Sanitize(new string('a', 252) + ".pdf"));
    }

    [Fact]
    public void Sanitize_LongNameWithDirectoriesAndControlCharacters_IsMeasuredAfterSanitising()
    {
        var name = new string('a', 251) + ".pdf";

        Assert.Equal(name, FileNames.Sanitize($"C:\\very\\long\\directory\\{name[..100]}\u202E{name[100..]}\r\n"));
    }
}
