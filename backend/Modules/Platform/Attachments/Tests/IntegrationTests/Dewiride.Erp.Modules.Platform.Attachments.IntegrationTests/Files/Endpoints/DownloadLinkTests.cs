using System.Net;
using System.Net.Http.Headers;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints;

public sealed class DownloadLinkTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string LinkParameter = "?link=";

    [Fact]
    public async Task Post_DownloadLinks_ReturnsTheContentPathValidForTheLinkLifetime()
    {
        using var client = factory.CreateClient();
        var attachment = await AttachmentsApi.UploadAsync(client, SampleFiles.Pdf(AttachmentsApi.UniqueToken()), SampleFiles.PdfType, "statement.pdf");
        var lifetime = factory.Services.GetRequiredService<IOptions<AttachmentsOptions>>().Value.DownloadLinkLifetime;
        var before = TimeProvider.System.GetUtcNow();

        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);

        var after = TimeProvider.System.GetUtcNow();
        Assert.StartsWith($"{AttachmentsApi.Route}/{attachment.Id}/content{LinkParameter}", link.Url, StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, Token(link.Url));
        Assert.InRange(link.ExpiresAt, before + lifetime, after + lifetime);
    }

    [Fact(Skip = "Defect: TransferEndpoints.CreateDownloadLinkAsync cuts the request path at its last '/', so a request ending in a slash answers a link under /download-links/content that no route serves.")]
    public async Task Post_DownloadLinksWithATrailingSlash_ReturnsAContentPathThatServesTheFile()
    {
        using var client = factory.CreateClient();
        var text = SampleFiles.Text($"trailing slash {AttachmentsApi.UniqueToken()}");
        var attachment = await AttachmentsApi.UploadAsync(client, text, SampleFiles.TextType, "slash.txt");
        using var created = await client.PostAsync(AttachmentsApi.Path($"/{attachment.Id}/download-links/"), content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var link = await created.Content.ReadFromJsonAsync<DownloadLinkResponse>(AttachmentsApi.Json, TestContext.Current.CancellationToken);
        Assert.NotNull(link);

        using var response = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal($"{AttachmentsApi.Route}/{attachment.Id}/content", link.Url[..link.Url.IndexOf('?', StringComparison.Ordinal)]);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(text, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Post_DownloadLinksTwice_ReturnsDistinctTokens()
    {
        using var client = factory.CreateClient();
        var attachment = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"two links {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, "links.txt");

        var first = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);
        var second = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);

        Assert.NotEqual(Token(first.Url), Token(second.Url));
    }

    [Fact]
    public async Task Post_DownloadLinksForAnUnknownId_AnswersNotFound()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(AttachmentsApi.Path($"/{Guid.CreateVersion7()}/download-links"), content: null, TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, AttachmentErrors.NotFound.Code);
    }

    [Fact]
    public async Task Get_ContentWithTheLink_StreamsTheOriginalBytesAsAnAttachment()
    {
        using var client = factory.CreateClient();
        var token = AttachmentsApi.UniqueToken();
        var pdf = SampleFiles.Pdf(token);
        var fileName = $"statement-{token}.pdf";
        var attachment = await AttachmentsApi.UploadAsync(client, pdf, SampleFiles.PdfType, fileName);
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);

        using var response = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(pdf, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(SampleFiles.PdfType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(pdf.Length, response.Content.Headers.ContentLength);
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.Equal("attachment", disposition.DispositionType);
        Assert.Equal(fileName, disposition.FileName?.Trim('"'));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task Get_ContentOfANonAsciiFileName_NamesItInTheExtendedFileNameParameter()
    {
        using var client = factory.CreateClient();
        const string FileName = "प्रस्ताव-résumé.txt";
        var text = SampleFiles.Text($"नमस्ते, résumé {AttachmentsApi.UniqueToken()}");
        var attachment = await AttachmentsApi.UploadAsync(client, text, SampleFiles.TextType, FileName);
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);

        using var response = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(FileName, attachment.FileName);
        Assert.Equal(FileName, response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(text, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Get_ContentOfAFileNameWithADoubleQuote_NamesItExactly()
    {
        using var client = factory.CreateClient();
        const string FileName = "quote\"d.txt";
        var file = new ByteArrayContent(SampleFiles.Text($"quoted {AttachmentsApi.UniqueToken()}"));
        file.Headers.ContentType = new MediaTypeHeaderValue(SampleFiles.TextType);
        file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"file\"", FileNameStar = FileName };
        using var content = new MultipartFormDataContent();
        content.Add(file);
        using var upload = await client.PostAsync(AttachmentsApi.Path(), content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var attachment = await upload.Content.ReadFromJsonAsync<AttachmentResponse>(AttachmentsApi.Json, TestContext.Current.CancellationToken);
        Assert.NotNull(attachment);
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);

        using var response = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(FileName, attachment.FileName);
        Assert.Equal(FileName, response.Content.Headers.ContentDisposition?.FileNameStar);
    }

    [Theory]
    [InlineData("semi;colon.txt")]
    [InlineData("comma, and space.txt")]
    [InlineData("per%20cent.txt")]
    public async Task Get_ContentOfAFileNameWithHeaderDelimiters_NamesItExactly(string fileName)
    {
        using var client = factory.CreateClient();
        var attachment = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"delimiters {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, fileName);
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);

        using var response = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(fileName, attachment.FileName);
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.Equal("attachment", disposition.DispositionType);
        Assert.Equal(fileName, disposition.FileNameStar);
    }

    [Fact]
    public async Task Get_ContentOfAFileOfSeveralHundredKilobytes_StreamsEveryByte()
    {
        using var client = factory.CreateClient();
        var token = AttachmentsApi.UniqueToken();
        var text = SampleFiles.Text(string.Concat(Enumerable.Range(0, 20_000).Select(line => $"{token} line {line}\n")));
        var attachment = await AttachmentsApi.UploadAsync(client, text, SampleFiles.TextType, "ledger.txt");
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);

        using var response = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(text.Length, response.Content.Headers.ContentLength);
        Assert.Equal(text, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Get_ContentOfASecondUploadOfTheSameBytes_StreamsThoseBytesUnderItsOwnName()
    {
        using var client = factory.CreateClient();
        var text = SampleFiles.Text($"shared content {AttachmentsApi.UniqueToken()}");
        await AttachmentsApi.UploadAsync(client, text, SampleFiles.TextType, "original.txt");
        var duplicate = await AttachmentsApi.UploadAsync(client, text, SampleFiles.TextType, "duplicate.txt");
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, duplicate.Id);

        using var response = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(text, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal("duplicate.txt", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    [Fact]
    public async Task Get_ContentTwiceWithinTheLinkLifetime_ServesBothTimes()
    {
        using var client = factory.CreateClient();
        var text = SampleFiles.Text($"read twice {AttachmentsApi.UniqueToken()}");
        var attachment = await AttachmentsApi.UploadAsync(client, text, SampleFiles.TextType, "twice.txt");
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);
        using var first = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(text, await second.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Get_ContentWithATamperedLink_AnswersNotFound()
    {
        using var client = factory.CreateClient();
        var attachment = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"tampered {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, "tampered.txt");
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);
        var token = Token(link.Url);
        var tampered = (token[0] == 'A' ? 'B' : 'A') + token[1..];

        using var response = await client.GetAsync(ContentPath(attachment.Id, tampered), TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, AttachmentErrors.NotFound.Code);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("not-a-token-of-the-right-length-at-all-but-still-short")]
    public async Task Get_ContentWithATokenOfTheWrongShape_AnswersNotFound(string token)
    {
        using var client = factory.CreateClient();
        var attachment = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"wrong shape {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, "shape.txt");

        using var response = await client.GetAsync(ContentPath(attachment.Id, token), TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, AttachmentErrors.NotFound.Code);
    }

    [Fact]
    public async Task Get_ContentOfAnotherAttachmentWithThisLink_AnswersNotFound()
    {
        using var client = factory.CreateClient();
        var linked = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"linked {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, "linked.txt");
        var other = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"other {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, "other.txt");
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, linked.Id);

        using var response = await client.GetAsync(ContentPath(other.Id, Token(link.Url)), TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, AttachmentErrors.NotFound.Code);
    }

    [Fact]
    public async Task Get_ContentAfterTheAttachmentWasDeleted_AnswersNotFound()
    {
        using var client = factory.CreateClient();
        var attachment = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"deleted {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, "deleted.txt");
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);
        using var deleted = await client.DeleteAsync(AttachmentsApi.Path($"/{attachment.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var response = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, AttachmentErrors.NotFound.Code);
    }

    [Fact]
    public async Task Post_DownloadLinksForADeletedAttachment_AnswersNotFound()
    {
        using var client = factory.CreateClient();
        var attachment = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"gone {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, "gone.txt");
        using var deleted = await client.DeleteAsync(AttachmentsApi.Path($"/{attachment.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var response = await client.PostAsync(AttachmentsApi.Path($"/{attachment.Id}/download-links"), content: null, TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, AttachmentErrors.NotFound.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData(LinkParameter)]
    public async Task Get_ContentWithoutALink_AnswersValidationProblemForTheLink(string query)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(AttachmentsApi.Path($"/{Guid.CreateVersion7()}/content{query}"), TestContext.Current.CancellationToken);

        await AssertLinkValidationProblemAsync(response);
    }

    [Fact]
    public async Task Get_ContentWithALinkOverSixtyFourCharacters_AnswersValidationProblemForTheLink()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(ContentPath(Guid.CreateVersion7(), new string('A', 65)), TestContext.Current.CancellationToken);

        await AssertLinkValidationProblemAsync(response);
    }

    private static string Token(string url) => Uri.UnescapeDataString(url[(url.IndexOf(LinkParameter, StringComparison.Ordinal) + LinkParameter.Length)..]);

    private static Uri ContentPath(Guid id, string token) => AttachmentsApi.Path($"/{id}/content{LinkParameter}{Uri.EscapeDataString(token)}");

    private static async Task AssertLinkValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("/problems/request.invalid", problem.Type);
        Assert.Equal("request.invalid", problem.Extensions["code"]?.ToString());
        Assert.Equal("link", Assert.Single(problem.Errors).Key);
    }
}
