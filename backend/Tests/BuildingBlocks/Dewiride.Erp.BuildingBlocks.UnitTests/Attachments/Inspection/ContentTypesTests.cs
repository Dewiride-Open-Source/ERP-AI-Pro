using System.Text;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Attachments.Inspection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Inspection;

public sealed class ContentTypesTests
{
    private static readonly string[] OneExtensionPerType = [".pdf", ".png", ".jpg", ".gif", ".webp", ".txt", ".csv", ".xlsx", ".docx"];

    [Theory]
    [InlineData("application/pdf", "application/pdf")]
    [InlineData("Application/PDF", "application/pdf")]
    [InlineData("text/plain; charset=utf-8", "text/plain")]
    [InlineData("  image/png  ", "image/png")]
    [InlineData(" TEXT/CSV ;header=present", "text/csv")]
    [InlineData("; charset=utf-8", "")]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_DeclaredType_DropsParametersCaseAndBlanks(string? declared, string expected)
    {
        Assert.Equal(expected, ContentTypes.Normalize(declared));
    }

    [Theory]
    [InlineData(ContentTypes.Pdf, "statement.pdf")]
    [InlineData(ContentTypes.Pdf, "STATEMENT.PDF")]
    [InlineData(ContentTypes.Png, "pixel.png")]
    [InlineData(ContentTypes.Jpeg, "photo.jpg")]
    [InlineData(ContentTypes.Jpeg, "photo.jpeg")]
    [InlineData(ContentTypes.Gif, "animation.gif")]
    [InlineData(ContentTypes.Webp, "image.webp")]
    [InlineData(ContentTypes.PlainText, "notes.txt")]
    [InlineData(ContentTypes.PlainText, "release.notes.txt")]
    [InlineData(ContentTypes.Csv, "rates.csv")]
    [InlineData(ContentTypes.Xlsx, "ledger.xlsx")]
    [InlineData(ContentTypes.Docx, "agreement.docx")]
    public void NameMatches_ExtensionOfTheType_IsAccepted(string contentType, string fileName)
    {
        Assert.True(ContentTypes.NameMatches(contentType, fileName));
    }

    [Theory]
    [InlineData(ContentTypes.PlainText, "payslip.hta")]
    [InlineData(ContentTypes.PlainText, "script.js")]
    [InlineData(ContentTypes.PlainText, "page.html")]
    [InlineData(ContentTypes.PlainText, "notes")]
    [InlineData(ContentTypes.PlainText, "notes.txt.")]
    [InlineData(ContentTypes.PlainText, "notes.txt.url")]
    [InlineData(ContentTypes.Pdf, "statement.pdf.exe")]
    [InlineData(ContentTypes.Png, "pixel.jpg")]
    [InlineData(ContentTypes.Docx, "macros.docm")]
    [InlineData(ContentTypes.Xlsx, "tool.jar")]
    [InlineData("application/zip", "archive.zip")]
    public void NameMatches_OtherExtensionOrNone_IsRefused(string contentType, string fileName)
    {
        Assert.False(ContentTypes.NameMatches(contentType, fileName));
    }

    [Fact]
    public void NameMatches_EveryKnownType_HasAnExtension()
    {
        Assert.All(ContentTypes.Known, contentType => Assert.Contains(OneExtensionPerType, extension => ContentTypes.NameMatches(contentType, $"file{extension}")));
    }

    [Theory]
    [InlineData(ContentTypes.PlainText, true)]
    [InlineData(ContentTypes.Csv, true)]
    [InlineData(ContentTypes.Pdf, false)]
    [InlineData(ContentTypes.Docx, false)]
    public void IsText_KnownType_IsTrueOnlyForTheTextTypes(string contentType, bool expected)
    {
        Assert.Equal(expected, ContentTypes.IsText(contentType));
    }

    [Fact]
    public void TryParseAllowList_ValidList_ReturnsTheNormalizedTypes()
    {
        string[] expected = ["application/pdf", "image/png", "text/plain"];

        var parsed = ContentTypes.TryParseAllowList(" application/pdf ; IMAGE/PNG;;Text/Plain ;", out var allowed, out var problem);

        Assert.True(parsed);
        Assert.Null(problem);
        Assert.Equal(expected.Order(), allowed!.Order());
    }

    [Fact]
    public void TryParseAllowList_DefaultSetting_AllowsEveryCheckedType()
    {
        var parsed = ContentTypes.TryParseAllowList(new AttachmentsOptions().AllowedContentTypes, out var allowed, out _);

        Assert.True(parsed);
        Assert.Equal(ContentTypes.Known.Order(), allowed!.Order());
    }

    [Theory]
    [InlineData("application/zip")]
    [InlineData("application/pdf;text/html")]
    [InlineData("image/png; image/svg+xml ;image/gif")]
    public void TryParseAllowList_TypeWithoutAContentCheck_FailsNamingIt(string value)
    {
        var unknown = value.Split(';').Select(entry => entry.Trim()).First(entry => !ContentTypes.Known.Contains(entry));

        var parsed = ContentTypes.TryParseAllowList(value, out var allowed, out var problem);

        Assert.False(parsed);
        Assert.Null(allowed);
        Assert.Contains($"names '{unknown}', which has no content check", problem, StringComparison.Ordinal);
        Assert.Contains(string.Join(", ", ContentTypes.Known), problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ; ;  ")]
    public void TryParseAllowList_NoTypes_FailsAskingForOne(string? value)
    {
        var parsed = ContentTypes.TryParseAllowList(value, out _, out var problem);

        Assert.False(parsed);
        Assert.Equal($"{AttachmentsOptions.SectionName}:AllowedContentTypes must name at least one content type, separated by ';'.", problem);
    }

    [Theory]
    [InlineData(ContentTypes.Pdf, "255044462D312E370A25")]
    [InlineData(ContentTypes.Png, "89504E470D0A1A0A0000000D49484452")]
    [InlineData(ContentTypes.Jpeg, "FFD8FFE000104A464946")]
    [InlineData(ContentTypes.Jpeg, "FFD8FFE1")]
    [InlineData(ContentTypes.Gif, "474946383761")]
    [InlineData(ContentTypes.Gif, "4749463839610100")]
    [InlineData(ContentTypes.Webp, "52494646240000005745425056503820")]
    [InlineData(ContentTypes.Webp, "524946460000000057454250")]
    [InlineData(ContentTypes.Xlsx, "504B030414000600")]
    [InlineData(ContentTypes.Docx, "504B030414000600")]
    public void Matches_GenuineSignature_IsAccepted(string contentType, string headHex)
    {
        Assert.True(ContentTypes.Matches(contentType, Convert.FromHexString(headHex), isWholeFile: false));
    }

    [Theory]
    [InlineData(ContentTypes.Pdf, "89504E470D0A1A0A")]
    [InlineData(ContentTypes.Pdf, "25504446")]
    [InlineData(ContentTypes.Pdf, "20255044462D")]
    [InlineData(ContentTypes.Png, "89504E470D0A1A")]
    [InlineData(ContentTypes.Png, "FFD8FFE0")]
    [InlineData(ContentTypes.Jpeg, "FFD8")]
    [InlineData(ContentTypes.Jpeg, "255044462D")]
    [InlineData(ContentTypes.Gif, "474946383861")]
    [InlineData(ContentTypes.Gif, "89504E470D0A1A0A")]
    [InlineData(ContentTypes.Webp, "52494646240000005741564566")]
    [InlineData(ContentTypes.Webp, "5249464624000000574542")]
    [InlineData(ContentTypes.Xlsx, "504B0506")]
    [InlineData(ContentTypes.Docx, "255044462D")]
    [InlineData("application/zip", "504B030414000600")]
    [InlineData("text/html", "3C68746D6C3E")]
    public void Matches_WrongOrMissingSignature_IsRefused(string contentType, string headHex)
    {
        Assert.False(ContentTypes.Matches(contentType, Convert.FromHexString(headHex), isWholeFile: false));
    }

    [Theory]
    [InlineData(ContentTypes.PlainText, true)]
    [InlineData(ContentTypes.PlainText, false)]
    [InlineData(ContentTypes.Csv, true)]
    [InlineData(ContentTypes.Csv, false)]
    public void Matches_Utf8Text_IsAccepted(string contentType, bool isWholeFile)
    {
        Assert.True(ContentTypes.Matches(contentType, Encoding.UTF8.GetBytes("Invoice,Amount\r\nINV-001,\u20B9 1\u00A0000\r\n"), isWholeFile));
    }

    [Theory]
    [InlineData(ContentTypes.PlainText)]
    [InlineData(ContentTypes.Csv)]
    public void Matches_Utf8TextWithAByteOrderMark_IsAccepted(string contentType)
    {
        Assert.True(ContentTypes.Matches(contentType, [0xEF, 0xBB, 0xBF, .. "name,amount"u8], isWholeFile: true));
    }

    [Theory]
    [InlineData(ContentTypes.PlainText, "61C328")]
    [InlineData(ContentTypes.PlainText, "FF616263")]
    [InlineData(ContentTypes.Csv, "E282612C62")]
    [InlineData(ContentTypes.Csv, "EDA080")]
    public void Matches_InvalidUtf8_IsRefused(string contentType, string headHex)
    {
        Assert.False(ContentTypes.Matches(contentType, Convert.FromHexString(headHex), isWholeFile: false));
    }

    [Theory]
    [InlineData(ContentTypes.PlainText)]
    [InlineData(ContentTypes.Csv)]
    public void Matches_TextWithANulByte_IsRefused(string contentType)
    {
        Assert.False(ContentTypes.Matches(contentType, [.. "name"u8, 0x00, .. "amount"u8], isWholeFile: true));
    }

    [Theory]
    [InlineData(ContentTypes.PlainText, false, true)]
    [InlineData(ContentTypes.PlainText, true, false)]
    [InlineData(ContentTypes.Csv, false, true)]
    [InlineData(ContentTypes.Csv, true, false)]
    public void Matches_MultiByteCharacterCutAtTheEndOfTheHead_IsAcceptedOnlyWhenMoreFollows(string contentType, bool isWholeFile, bool expected)
    {
        var euro = Encoding.UTF8.GetBytes("\u20AC");

        Assert.Equal(expected, ContentTypes.Matches(contentType, [.. "total "u8, euro[0], euro[1]], isWholeFile));
    }

    [Fact]
    public void Matches_EveryKnownTypeWithItsSignature_IsAccepted()
    {
        foreach (var contentType in ContentTypes.Known)
        {
            var genuine = contentType switch
            {
                ContentTypes.Pdf => "%PDF-"u8.ToArray(),
                ContentTypes.Png => Convert.FromHexString("89504E470D0A1A0A"),
                ContentTypes.Jpeg => Convert.FromHexString("FFD8FF"),
                ContentTypes.Gif => "GIF89a"u8.ToArray(),
                ContentTypes.Webp => "RIFF\0\0\0\0WEBP"u8.ToArray(),
                ContentTypes.Xlsx or ContentTypes.Docx => Convert.FromHexString("504B0304"),
                _ => "plain text"u8.ToArray(),
            };

            Assert.True(ContentTypes.Matches(contentType, genuine, isWholeFile: true), contentType);
        }
    }
}
