using System.Text;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Uploads;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.Modules.Platform.Attachments.UnitTests.Files.Endpoints.Uploads;

public sealed class MultipartFileReaderTests
{
    private const string Boundary = "erp-test-boundary";

    private const string FormData = $"multipart/form-data; boundary={Boundary}";

    [Fact]
    public async Task ReadFirstFileAsync_FieldBeforeTwoFiles_ReturnsTheFirstFilePart()
    {
        var request = Request(
            FormData,
            Body(
                Boundary,
                Part("not a file", "Content-Disposition: form-data; name=\"note\""),
                Part("first file", "Content-Disposition: form-data; name=\"file\"; filename=\"first.txt\"", "Content-Type: text/plain"),
                Part("second file", "Content-Disposition: form-data; name=\"other\"; filename=\"second.pdf\"", "Content-Type: application/pdf")));

        var result = await MultipartFileReader.ReadFirstFileAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("first.txt", result.Value.FileName);
        Assert.Equal("text/plain", result.Value.ContentType);
        Assert.Equal("first file", await ReadAllAsync(result.Value.Content));
    }

    [Fact]
    public async Task ReadFirstFileAsync_BothFileNameParameters_PrefersTheExtendedName()
    {
        var request = Request(
            FormData,
            Body(Boundary, Part("text", "Content-Disposition: form-data; name=\"file\"; filename=\"fallback.txt\"; filename*=UTF-8''r%C3%A9sum%C3%A9.txt")));

        var result = await MultipartFileReader.ReadFirstFileAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("résumé.txt", result.Value.FileName);
    }

    [Theory]
    [InlineData("filename=\"quoted name.pdf\"", "quoted name.pdf")]
    [InlineData("filename=token.pdf", "token.pdf")]
    [InlineData("filename*=UTF-8''%E0%A4%AA%E0%A5%8D%E0%A4%B0%E0%A4%B8%E0%A5%8D%E0%A4%A4%E0%A4%BE%E0%A4%B5.pdf", "प्रस्ताव.pdf")]
    public async Task ReadFirstFileAsync_OneFileNameParameter_ReturnsTheNameWithoutQuotesOrEncoding(string parameter, string expected)
    {
        var request = Request(FormData, Body(Boundary, Part("content", $"Content-Disposition: form-data; name=\"file\"; {parameter}")));

        var result = await MultipartFileReader.ReadFirstFileAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.FileName);
    }

    [Fact]
    public async Task ReadFirstFileAsync_FilePartWithoutContentType_ReturnsNoContentType()
    {
        var request = Request(FormData, Body(Boundary, Part("content", "Content-Disposition: form-data; name=\"file\"; filename=\"untyped.bin\"")));

        var result = await MultipartFileReader.ReadFirstFileAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ContentType);
    }

    [Theory]
    [InlineData("\"erp quoted boundary\"", "erp quoted boundary")]
    [InlineData("\"erp-quoted-boundary\"", "erp-quoted-boundary")]
    public async Task ReadFirstFileAsync_QuotedBoundary_ReadsTheFile(string declared, string boundary)
    {
        var request = Request($"multipart/form-data; boundary={declared}", Body(boundary, FilePart("quoted")));

        var result = await MultipartFileReader.ReadFirstFileAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("quoted", await ReadAllAsync(result.Value.Content));
    }

    [Fact]
    public async Task ReadFirstFileAsync_MediaTypeInMixedCase_ReadsTheFile()
    {
        var request = Request($"Multipart/Form-Data; boundary={Boundary}", Body(Boundary, FilePart("mixed case")));

        var result = await MultipartFileReader.ReadFirstFileAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("mixed case", await ReadAllAsync(result.Value.Content));
    }

    [Fact]
    public async Task ReadFirstFileAsync_BoundaryOfSeventyCharacters_ReadsTheFile()
    {
        var boundary = new string('b', 70);
        var request = Request($"multipart/form-data; boundary={boundary}", Body(boundary, FilePart("longest boundary")));

        var result = await MultipartFileReader.ReadFirstFileAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("longest boundary", await ReadAllAsync(result.Value.Content));
    }

    [Fact]
    public async Task ReadFirstFileAsync_BoundaryOverSeventyCharacters_ReturnsMultipartRequiredWithoutReadingTheBody()
    {
        var boundary = new string('b', 71);
        var request = Request($"multipart/form-data; boundary={boundary}", Body(boundary, FilePart("too long a boundary")));

        var result = await MultipartFileReader.ReadFirstFileAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AttachmentErrors.MultipartRequired, result.Error);
        Assert.Equal(0, request.Body.Position);
    }

    [Theory]
    [InlineData("multipart/form-data")]
    [InlineData("multipart/form-data; charset=utf-8")]
    [InlineData("multipart/form-data; boundary=\"\"")]
    [InlineData("multipart/form-data; boundary=\"   \"")]
    public async Task ReadFirstFileAsync_NoBoundary_ReturnsMultipartRequiredWithoutReadingTheBody(string contentType)
    {
        var request = Request(contentType, Body(Boundary, FilePart("unreachable")));

        var result = await MultipartFileReader.ReadFirstFileAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AttachmentErrors.MultipartRequired, result.Error);
        Assert.Equal(0, request.Body.Position);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a media type")]
    [InlineData("application/json")]
    [InlineData("application/x-www-form-urlencoded")]
    [InlineData($"multipart/mixed; boundary={Boundary}")]
    [InlineData($"text/plain; boundary={Boundary}")]
    public async Task ReadFirstFileAsync_ContentTypeOtherThanMultipartFormData_ReturnsMultipartRequiredWithoutReadingTheBody(string? contentType)
    {
        var request = Request(contentType, Body(Boundary, FilePart("unreachable")));

        var result = await MultipartFileReader.ReadFirstFileAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AttachmentErrors.MultipartRequired, result.Error);
        Assert.Equal(0, request.Body.Position);
    }

    [Theory]
    [InlineData("Content-Disposition: form-data; name=\"note\"")]
    [InlineData("Content-Disposition: attachment; filename=\"not-form-data.txt\"")]
    [InlineData("Content-Type: text/plain")]
    public async Task ReadFirstFileAsync_PartsWithoutAFormDataFileName_ReturnsFileMissing(string headers)
    {
        var request = Request(FormData, Body(Boundary, Part("value", headers), Part("another value", "Content-Disposition: form-data; name=\"second\"")));

        var result = await MultipartFileReader.ReadFirstFileAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AttachmentErrors.FileMissing, result.Error);
    }

    [Fact]
    public async Task ReadFirstFileAsync_NoPartsAtAll_ReturnsFileMissing()
    {
        var request = Request(FormData, Body(Boundary));

        var result = await MultipartFileReader.ReadFirstFileAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AttachmentErrors.FileMissing, result.Error);
    }

    [Fact]
    public async Task ReadFirstFileAsync_BodyEndingBeforeTheFirstBoundary_ThrowsIOException()
    {
        var request = Request(FormData, "a preamble that never reaches a boundary");

        await Assert.ThrowsAsync<IOException>(() => MultipartFileReader.ReadFirstFileAsync(request, TestContext.Current.CancellationToken));
    }

    private static HttpRequest Request(string? contentType, string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        return context.Request;
    }

    private static string Body(string boundary, params string[] parts) =>
        string.Concat(parts.Select(part => $"--{boundary}\r\n{part}")) + $"--{boundary}--\r\n";

    private static string Part(string content, params string[] headers) => $"{string.Join("\r\n", headers)}\r\n\r\n{content}\r\n";

    private static string FilePart(string content) =>
        Part(content, "Content-Disposition: form-data; name=\"file\"; filename=\"file.txt\"", "Content-Type: text/plain");

    private static async Task<string> ReadAllAsync(Stream content)
    {
        using var reader = new StreamReader(content, Encoding.UTF8);

        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }
}
