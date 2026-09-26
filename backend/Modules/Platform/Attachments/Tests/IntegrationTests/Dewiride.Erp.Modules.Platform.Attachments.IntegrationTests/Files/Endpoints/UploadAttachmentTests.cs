using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Responses;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints;

public sealed class UploadAttachmentTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task Post_Png_AnswersCreatedWithTheLocationAndTheStoredDetails()
    {
        using var client = factory.CreateClient();
        var png = SampleFiles.OnePixelPng();
        var fileName = $"pixel-{AttachmentsApi.UniqueToken()}.png";
        var before = TimeProvider.System.GetUtcNow();

        using var response = await AttachmentsApi.PostFileAsync(client, png, SampleFiles.PngType, fileName);

        var after = TimeProvider.System.GetUtcNow();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var attachment = body.RootElement;
        Assert.Equal(
            ["contentType", "createdAt", "createdBy", "fileName", "id", "scanStatus", "sha256", "sizeBytes"],
            attachment.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        var id = attachment.GetProperty("id").GetGuid();
        Assert.Equal(7, id.Version);
        Assert.Equal($"{AttachmentsApi.Route}/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(fileName, attachment.GetProperty("fileName").GetString());
        Assert.Equal(SampleFiles.PngType, attachment.GetProperty("contentType").GetString());
        Assert.Equal(png.Length, attachment.GetProperty("sizeBytes").GetInt64());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(png)), attachment.GetProperty("sha256").GetString());
        Assert.Equal("notScanned", attachment.GetProperty("scanStatus").GetString());
        Assert.InRange(attachment.GetProperty("createdAt").GetDateTimeOffset(), before, after);
        Assert.Equal(ActorIds.Anonymous, attachment.GetProperty("createdBy").GetGuid());
    }

    [Fact]
    public async Task Post_TextWithACharsetParameter_StoresTheBareMediaType()
    {
        using var client = factory.CreateClient();

        var attachment = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"plain text {AttachmentsApi.UniqueToken()}"), "text/plain; charset=utf-8", "notes.txt");

        Assert.Equal(SampleFiles.TextType, attachment.ContentType);
    }

    [Theory]
    [InlineData("reports/2026/invoice.pdf")]
    [InlineData("C:\\fakepath\\invoice.pdf")]
    public async Task Post_FileNameWithDirectories_KeepsOnlyTheFileName(string fileName)
    {
        using var client = factory.CreateClient();

        var attachment = await AttachmentsApi.UploadAsync(client, SampleFiles.Pdf(AttachmentsApi.UniqueToken()), SampleFiles.PdfType, fileName);

        Assert.Equal("invoice.pdf", attachment.FileName);
    }

    [Fact]
    public async Task Post_SameContentTwice_CreatesTwoAttachmentsWithTheSameDigest()
    {
        using var client = factory.CreateClient();
        var text = SampleFiles.Text($"identical content {AttachmentsApi.UniqueToken()}");

        var first = await AttachmentsApi.UploadAsync(client, text, SampleFiles.TextType, "first.txt");
        var second = await AttachmentsApi.UploadAsync(client, text, SampleFiles.TextType, "second.txt");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(first.SizeBytes, second.SizeBytes);
        Assert.Equal("second.txt", second.FileName);
    }

    [Fact]
    public async Task Post_JsonBody_AnswersMultipartRequired()
    {
        using var client = factory.CreateClient();
        using var content = JsonContent.Create(new { fileName = "invoice.pdf" });

        using var response = await client.PostAsync(AttachmentsApi.Path(), content, TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "attachment.multipart-required");
    }

    [Fact]
    public async Task Post_MultipartWithoutAFilePart_AnswersFileMissing()
    {
        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("only a note"), "note");

        using var response = await client.PostAsync(AttachmentsApi.Path(), content, TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, "attachment.file-missing");
    }

    [Fact]
    public async Task Post_EmptyFile_AnswersEmpty()
    {
        using var client = factory.CreateClient();

        using var response = await AttachmentsApi.PostFileAsync(client, [], SampleFiles.TextType, "empty.txt");

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, "attachment.empty");
    }

    [Theory]
    [InlineData("application/zip")]
    [InlineData("text/html")]
    [InlineData("application/octet-stream")]
    [InlineData(null)]
    public async Task Post_DeclaredTypeOutsideTheAllowList_AnswersUnsupportedType(string? contentType)
    {
        using var client = factory.CreateClient();

        using var response = await AttachmentsApi.PostFileAsync(client, SampleFiles.Text("<html></html>"), contentType, "page.html");

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "attachment.unsupported-type");
    }

    [Theory]
    [InlineData(SampleFiles.PngType)]
    [InlineData(SampleFiles.PdfType)]
    [InlineData("image/jpeg")]
    public async Task Post_TextDeclaredAsABinaryType_AnswersContentMismatch(string contentType)
    {
        using var client = factory.CreateClient();

        using var response = await AttachmentsApi.PostFileAsync(client, SampleFiles.Text("this is plain text, not an image"), contentType, "disguised.png");

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "attachment.content-mismatch");
    }

    [Fact]
    public async Task Post_PngDeclaredAsText_AnswersContentMismatch()
    {
        using var client = factory.CreateClient();

        using var response = await AttachmentsApi.PostFileAsync(client, SampleFiles.OnePixelPng(), SampleFiles.TextType, "pixel.txt");

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "attachment.content-mismatch");
    }

    [Theory]
    [InlineData("...")]
    [InlineData("reports/")]
    public async Task Post_FileNameWithoutAVisibleName_AnswersFileNameInvalid(string fileName)
    {
        using var client = factory.CreateClient();

        using var response = await AttachmentsApi.PostFileAsync(client, SampleFiles.Text("named badly"), SampleFiles.TextType, fileName);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, "attachment.file-name-invalid");
    }

    [Fact]
    public async Task Post_FileNameOf255Characters_KeepsTheWholeName()
    {
        using var client = factory.CreateClient();
        var fileName = new string('n', 251) + ".txt";

        var attachment = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"long name {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, fileName);

        Assert.Equal(fileName, attachment.FileName);
    }

    [Fact]
    public async Task Post_FileNameOver255Characters_AnswersFileNameInvalid()
    {
        using var client = factory.CreateClient();

        using var response = await AttachmentsApi.PostFileAsync(client, SampleFiles.Text("too long a name"), SampleFiles.TextType, new string('n', 252) + ".txt");

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, "attachment.file-name-invalid");
    }

    [Fact]
    public async Task Post_BodyEndingBeforeTheClosingBoundary_AnswersIncomplete()
    {
        using var client = factory.CreateClient();
        const string Boundary = "erp-truncated-upload";
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(
            $"--{Boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"cut.txt\"\r\nContent-Type: text/plain\r\n\r\nthe upload stops here"));
        content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={Boundary}");

        using var response = await client.PostAsync(AttachmentsApi.Path(), content, TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, AttachmentErrors.Incomplete.Code);
    }

    [Fact]
    public async Task Post_Upload_ReturnsTheSameDetailsAsAGetOfTheLocation()
    {
        using var client = factory.CreateClient();
        using var upload = await AttachmentsApi.PostFileAsync(client, SampleFiles.Pdf(AttachmentsApi.UniqueToken()), SampleFiles.PdfType, "statement.pdf");
        var created = await upload.Content.ReadFromJsonAsync<AttachmentResponse>(AttachmentsApi.Json, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync(upload.Headers.Location, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created, await response.Content.ReadFromJsonAsync<AttachmentResponse>(AttachmentsApi.Json, TestContext.Current.CancellationToken));
    }
}
