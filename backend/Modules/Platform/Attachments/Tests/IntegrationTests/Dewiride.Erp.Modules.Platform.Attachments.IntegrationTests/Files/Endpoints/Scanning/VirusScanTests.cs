using System.Net;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Scanning;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints.Scanning;

public sealed class VirusScanTests
{
    [Fact]
    public async Task Post_ScannerReportsClean_StoresTheAttachmentAsCleanAfterScanningEveryByte()
    {
        var scanner = new FixedVerdictScanner(AttachmentScanVerdict.Clean);
        using var root = new ErpApiFactory();
        using var factory = WithScanner(root, scanner);
        using var client = factory.CreateClient();
        var token = AttachmentsApi.UniqueToken();
        var text = SampleFiles.Text(string.Concat(Enumerable.Range(0, 10_000).Select(line => $"{token} scanned line {line}\n")));

        var attachment = await AttachmentsApi.UploadAsync(client, text, SampleFiles.TextType, "scanned.txt");

        Assert.Equal(AttachmentScanStatus.Clean, attachment.ScanStatus);
        Assert.Equal(text, Assert.Single(scanner.Scanned));
    }

    [Fact]
    public async Task Post_ScannerReportsInfected_AnswersInfectedAndStoresNothing()
    {
        var scanner = new FixedVerdictScanner(AttachmentScanVerdict.Infected);
        using var root = new ErpApiFactory();
        using var factory = WithScanner(root, scanner);
        using var client = factory.CreateClient();
        var token = AttachmentsApi.UniqueToken();

        using var response = await AttachmentsApi.PostFileAsync(client, SampleFiles.Text($"infected {token}"), SampleFiles.TextType, $"{token}-infected.txt");

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, AttachmentErrors.Infected.Code);
        Assert.Single(scanner.Scanned);
        var list = await client.GetFromJsonAsync<PagedResponse<AttachmentResponse>>(
            AttachmentsApi.Path($"?filter={Uri.EscapeDataString($"fileName:contains:{token}")}"),
            AttachmentsApi.Json,
            TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        Assert.Equal(0, list.TotalCount);
    }

    private static WebApplicationFactory<Program> WithScanner(ErpApiFactory root, IAttachmentScanner scanner) =>
        root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton(scanner)));
}
