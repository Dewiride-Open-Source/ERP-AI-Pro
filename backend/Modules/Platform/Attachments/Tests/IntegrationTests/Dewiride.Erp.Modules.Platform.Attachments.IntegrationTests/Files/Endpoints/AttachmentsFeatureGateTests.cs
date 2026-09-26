using System.Net;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints;

public sealed class AttachmentsFeatureGateTests(AttachmentsFeatureGateTests.Fixture fixture) : IClassFixture<AttachmentsFeatureGateTests.Fixture>
{
    private const string FeatureDisabled = "feature.disabled";

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/policy")]
    [InlineData("GET", "/{id}")]
    [InlineData("DELETE", "/{id}")]
    [InlineData("POST", "/{id}/download-links")]
    [InlineData("GET", "/{id}/content?link=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Request_ModuleDisabled_AnswersFeatureDisabled(string method, string route)
    {
        using var client = fixture.Factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), AttachmentsApi.Path(route.Replace("{id}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal)));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, FeatureDisabled);
    }

    [Fact]
    public async Task Post_UploadWithTheModuleDisabled_AnswersFeatureDisabled()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await AttachmentsApi.PostFileAsync(client, SampleFiles.OnePixelPng(), SampleFiles.PngType, "pixel.png");

        var problem = await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, FeatureDisabled);
        Assert.Contains(AttachmentsModule.FeatureFlag, problem.Detail, StringComparison.Ordinal);
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithFeature(AttachmentsModule.FeatureFlag, enabled: false);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
