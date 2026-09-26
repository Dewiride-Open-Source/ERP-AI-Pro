using System.Net;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Attachments.Scanning;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints.Uploads;

public sealed class UploadConcurrencyTests
{
    [Fact]
    public async Task Post_WhileTheCappedNumberOfUploadsIsInFlight_AnswersTooManyRequestsWithRetryAfter()
    {
        var scanner = new HeldScanner();
        using var root = new ErpApiFactory().WithConfiguration($"{AttachmentsOptions.SectionName}:{nameof(AttachmentsOptions.MaxConcurrentUploads)}", "1");
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IAttachmentScanner>(scanner)));
        using var client = factory.CreateClient();
        var token = AttachmentsApi.UniqueToken();
        var first = AttachmentsApi.PostFileAsync(client, SampleFiles.Text($"first {token}"), SampleFiles.TextType, "first.txt");
        await scanner.Entered.WaitAsync(TestContext.Current.CancellationToken);

        using var second = await AttachmentsApi.PostFileAsync(client, SampleFiles.Text($"second {token}"), SampleFiles.TextType, "second.txt");

        await AttachmentsApi.AssertProblemAsync(second, HttpStatusCode.TooManyRequests, "rate-limit.exceeded");
        Assert.True(second.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        scanner.Release();
        using var firstResponse = await first;
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
    }

    [Fact]
    public async Task Post_AfterTheUploadInFlightFinished_IsAcceptedAgain()
    {
        var scanner = new HeldScanner();
        using var root = new ErpApiFactory().WithConfiguration($"{AttachmentsOptions.SectionName}:{nameof(AttachmentsOptions.MaxConcurrentUploads)}", "1");
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IAttachmentScanner>(scanner)));
        using var client = factory.CreateClient();
        var token = AttachmentsApi.UniqueToken();
        scanner.Release();
        using (var first = await AttachmentsApi.PostFileAsync(client, SampleFiles.Text($"first {token}"), SampleFiles.TextType, "first.txt"))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        using var second = await AttachmentsApi.PostFileAsync(client, SampleFiles.Text($"second {token}"), SampleFiles.TextType, "second.txt");

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }
}
