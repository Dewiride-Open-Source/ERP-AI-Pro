using System.Net;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints;

public sealed class DownloadLinkExpiryTests
{
    [Fact]
    public async Task Get_ContentOneTickBeforeTheLinkExpires_StreamsTheFile()
    {
        var time = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        using var root = new ErpApiFactory();
        using var factory = WithClock(root, time);
        using var client = factory.CreateClient();
        var text = SampleFiles.Text($"almost expired {AttachmentsApi.UniqueToken()}");
        var attachment = await AttachmentsApi.UploadAsync(client, text, SampleFiles.TextType, "almost.txt");
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);

        time.Advance(LinkLifetime(factory) - TimeSpan.FromTicks(1));
        using var response = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(text, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Get_ContentOnceTheLinkLifetimeHasPassed_AnswersNotFound()
    {
        var time = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        using var root = new ErpApiFactory();
        using var factory = WithClock(root, time);
        using var client = factory.CreateClient();
        var attachment = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"expired {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, "expired.txt");
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);
        Assert.Equal(time.GetUtcNow() + LinkLifetime(factory), link.ExpiresAt);

        time.Advance(LinkLifetime(factory));
        using var response = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, AttachmentErrors.NotFound.Code);
    }

    private static WebApplicationFactory<Program> WithClock(ErpApiFactory root, TimeProvider time) =>
        root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton(time)));

    private static TimeSpan LinkLifetime(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<IOptions<AttachmentsOptions>>().Value.DownloadLinkLifetime;
}
