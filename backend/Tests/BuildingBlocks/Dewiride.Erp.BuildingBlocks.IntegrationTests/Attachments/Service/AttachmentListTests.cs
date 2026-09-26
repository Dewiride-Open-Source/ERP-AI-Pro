using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Service;

public sealed class AttachmentListTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task ListAsync_NoSort_ReturnsTheNewestFirst()
    {
        var stamp = TestFiles.Stamp();
        await UploadAsync(stamp, "b", 1_000);
        await UploadAsync(stamp, "a", 3_000);
        await UploadAsync(stamp, "c", 2_000);

        var page = await ListAsync(null, null, null, $"fileName:contains:{stamp}");

        Assert.Equal(Names(stamp, "c", "a", "b"), page.Value.Items.Select(item => item.FileName));
    }

    [Theory]
    [InlineData("fileName", "a", "b", "c")]
    [InlineData("fileName:desc", "c", "b", "a")]
    [InlineData("sizeBytes", "b", "c", "a")]
    [InlineData("sizeBytes:desc", "a", "c", "b")]
    [InlineData("createdAt", "b", "a", "c")]
    public async Task ListAsync_SortTerm_OrdersByThatField(string sort, string first, string second, string third)
    {
        var stamp = TestFiles.Stamp();
        await UploadAsync(stamp, "b", 1_000);
        await UploadAsync(stamp, "a", 3_000);
        await UploadAsync(stamp, "c", 2_000);

        var page = await ListAsync(null, null, sort, $"fileName:contains:{stamp}");

        Assert.Equal(Names(stamp, first, second, third), page.Value.Items.Select(item => item.FileName));
    }

    [Fact]
    public async Task ListAsync_SecondPage_ReturnsThatPageWithTheTotalAcrossPages()
    {
        var stamp = TestFiles.Stamp();
        foreach (var name in new[] { "a", "b", "c", "d", "e" })
        {
            await UploadAsync(stamp, name, 1_000);
        }

        var page = (await ListAsync(2, 2, "fileName", $"fileName:contains:{stamp}")).Value;

        Assert.Equal(Names(stamp, "c", "d"), page.Items.Select(item => item.FileName));
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPrevious);
        Assert.True(page.HasNext);
    }

    [Fact]
    public async Task ListAsync_FileNameContains_ReturnsOnlyTheMatchingFiles()
    {
        var stamp = TestFiles.Stamp();
        await UploadAsync(stamp, "invoice-april", 1_000);
        await UploadAsync(stamp, "receipt-april", 1_000);
        await UploadAsync(stamp, "invoice-may", 1_000);

        var page = await ListAsync(null, null, "fileName", $"fileName:contains:{stamp}-invoice");

        Assert.Equal(Names(stamp, "invoice-april", "invoice-may"), page.Value.Items.Select(item => item.FileName));
    }

    [Fact]
    public async Task ListAsync_ContentTypeEquals_ReturnsOnlyFilesOfThatType()
    {
        var stamp = TestFiles.Stamp();
        await UploadAsync(stamp, "notes", 1_000);
        var rates = await factory.UploadAsync($"{stamp}-rates.csv", "text/csv", TestFiles.Text($"{stamp},18,IGST", 1_000));
        await factory.UploadAsync($"{stamp}-scan.pdf", TestFiles.PdfType, TestFiles.Pdf(1_000));

        var page = await ListAsync(null, null, null, $"fileName:contains:{stamp};contentType:eq:text/csv");

        Assert.Equal(rates, Assert.Single(page.Value.Items));
    }

    [Theory]
    [InlineData("uploadedBy", null)]
    [InlineData(null, "uploadedBy:eq:someone")]
    public async Task ListAsync_FieldOutsideTheAllowList_FailsWithTheInvalidFieldError(string? sort, string? filter)
    {
        var result = await ListAsync(null, null, sort, filter);

        Assert.Equal(QueryErrors.InvalidField, result.Error?.Code);
    }

    private static string[] Names(string stamp, params string[] names) => [.. names.Select(name => $"{stamp}-{name}.txt")];

    private Task<AttachmentDetails> UploadAsync(string stamp, string name, int length) =>
        factory.UploadAsync($"{stamp}-{name}.txt", TestFiles.TextType, TestFiles.Text($"{stamp}-{name}", length));

    private async Task<Result<PagedResult<AttachmentDetails>>> ListAsync(int? page, int? pageSize, string? sort, string? filter)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IAttachmentService>()
            .ListAsync(ListRequest.Parse(page, pageSize, sort, filter).Value, TestContext.Current.CancellationToken);
    }
}
