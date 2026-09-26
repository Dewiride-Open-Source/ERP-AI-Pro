using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Responses;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints;

public sealed class ListAttachmentsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task Get_List_FilteredByFileName_AnswersThePagingEnvelopeNewestFirst()
    {
        using var client = factory.CreateClient();
        var token = AttachmentsApi.UniqueToken();
        var (text, pdf, png) = await UploadThreeAsync(client, token);

        using var response = await client.GetAsync(ListPath($"filter={Filter($"fileName:contains:{token}")}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var envelope = body.RootElement;
        Assert.Equal(["items", "page", "pageSize", "totalCount", "totalPages"], envelope.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(1, envelope.GetProperty("page").GetInt32());
        Assert.Equal(50, envelope.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, envelope.GetProperty("totalCount").GetInt64());
        Assert.Equal(1, envelope.GetProperty("totalPages").GetInt32());
        var items = envelope.GetProperty("items").Deserialize<List<AttachmentResponse>>(AttachmentsApi.Json);
        Assert.Equal([png, pdf, text], items);
    }

    [Fact]
    public async Task Get_List_SortedByFileNameAscending_OrdersByName()
    {
        using var client = factory.CreateClient();
        var token = AttachmentsApi.UniqueToken();
        var (text, pdf, png) = await UploadThreeAsync(client, token);

        var page = await ListAsync(client, $"sort=fileName:asc&filter={Filter($"fileName:contains:{token}")}");

        Assert.Equal([pdf.Id, text.Id, png.Id], page.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Get_List_SortedBySizeDescending_OrdersByTheOriginalFileSize()
    {
        using var client = factory.CreateClient();
        var token = AttachmentsApi.UniqueToken();
        var (text, pdf, png) = await UploadThreeAsync(client, token);

        var page = await ListAsync(client, $"sort=sizeBytes:desc&filter={Filter($"fileName:contains:{token}")}");

        Assert.Equal(new[] { text, pdf, png }.OrderByDescending(item => item.SizeBytes).Select(item => item.Id), page.Items.Select(item => item.Id));
        Assert.Equal(3, page.Items.Select(item => item.SizeBytes).Distinct().Count());
    }

    [Fact]
    public async Task Get_List_FilteredByContentType_ReturnsOnlyThatType()
    {
        using var client = factory.CreateClient();
        var token = AttachmentsApi.UniqueToken();
        var (_, pdf, _) = await UploadThreeAsync(client, token);

        var page = await ListAsync(client, $"filter={Filter($"fileName:contains:{token};contentType:eq:{SampleFiles.PdfType}")}");

        Assert.Equal(pdf, Assert.Single(page.Items));
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Get_List_SecondPageOfTwo_ReturnsTheOldestAttachment()
    {
        using var client = factory.CreateClient();
        var token = AttachmentsApi.UniqueToken();
        var (text, _, _) = await UploadThreeAsync(client, token);

        var page = await ListAsync(client, $"page=2&pageSize=2&filter={Filter($"fileName:contains:{token}")}");

        Assert.Equal(text, Assert.Single(page.Items));
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
    }

    [Theory]
    [InlineData("sort=owner:asc", "query.invalid-field")]
    [InlineData("sort=fileName:sideways", "query.invalid-sort")]
    [InlineData("filter=sizeBytes:eq:1", "query.invalid-field")]
    [InlineData("filter=createdAt:contains:2026", "query.invalid-operator")]
    [InlineData("filter=createdAt:gt:yesterday", "query.invalid-value")]
    [InlineData("filter=fileName", "query.invalid-filter")]
    [InlineData("page=0", "query.invalid-page")]
    [InlineData("pageSize=201", "query.invalid-page")]
    public async Task Get_List_QueryOutsideTheAllowLists_AnswersTheQueryErrorCode(string query, string code)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(ListPath(query), TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, code);
    }

    private static Uri ListPath(string query) => AttachmentsApi.Path($"?{query}");

    private static string Filter(string filter) => Uri.EscapeDataString(filter);

    private static async Task<PagedResponse<AttachmentResponse>> ListAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync(ListPath(query), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<AttachmentResponse>>(AttachmentsApi.Json, TestContext.Current.CancellationToken);
        Assert.NotNull(page);

        return page;
    }

    private static async Task<(AttachmentResponse Text, AttachmentResponse Pdf, AttachmentResponse Png)> UploadThreeAsync(HttpClient client, string token)
    {
        var text = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"notes {token}"), SampleFiles.TextType, $"{token}-b-notes.txt");
        var pdf = await AttachmentsApi.UploadAsync(client, SampleFiles.Pdf(token), SampleFiles.PdfType, $"{token}-a-statement.pdf");
        var png = await AttachmentsApi.UploadAsync(client, SampleFiles.OnePixelPng(), SampleFiles.PngType, $"{token}-c-pixel.png");

        return (text, pdf, png);
    }
}
