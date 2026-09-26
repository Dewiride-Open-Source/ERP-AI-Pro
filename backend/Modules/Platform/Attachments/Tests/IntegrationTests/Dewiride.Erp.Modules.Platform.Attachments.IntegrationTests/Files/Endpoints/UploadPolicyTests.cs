using System.Globalization;
using System.Net;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Responses;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints;

public sealed class UploadPolicyTests(UploadPolicyTests.Fixture fixture) : IClassFixture<UploadPolicyTests.Fixture>
{
    private const int MaxSizeBytes = 1024;

    [Fact]
    public async Task Get_Policy_ReturnsTheConfiguredLimitAndTheAllowedTypesInOrder()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync(AttachmentsApi.Path("/policy"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var policy = await response.Content.ReadFromJsonAsync<UploadPolicyResponse>(AttachmentsApi.Json, TestContext.Current.CancellationToken);
        Assert.NotNull(policy);
        Assert.Equal(MaxSizeBytes, policy.MaxSizeBytes);
        Assert.Equal([SampleFiles.PdfType, SampleFiles.TextType], policy.AllowedContentTypes);
    }

    [Fact]
    public async Task Post_FileOfExactlyTheLimit_IsStored()
    {
        using var client = fixture.Factory.CreateClient();
        var text = SampleFiles.Text(AttachmentsApi.UniqueToken().PadRight(MaxSizeBytes, 'x'));

        var attachment = await AttachmentsApi.UploadAsync(client, text, SampleFiles.TextType, "exactly-the-limit.txt");

        Assert.Equal(MaxSizeBytes, attachment.SizeBytes);
    }

    [Fact]
    public async Task Post_FileOneByteOverTheLimit_AnswersTooLarge()
    {
        using var client = fixture.Factory.CreateClient();
        var text = SampleFiles.Text(AttachmentsApi.UniqueToken().PadRight(MaxSizeBytes + 1, 'x'));

        using var response = await AttachmentsApi.PostFileAsync(client, text, SampleFiles.TextType, "over-the-limit.txt");

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge, "attachment.too-large");
    }

    [Fact]
    public async Task Post_TypeLeftOutOfTheConfiguredAllowList_AnswersUnsupportedType()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await AttachmentsApi.PostFileAsync(client, SampleFiles.OnePixelPng(), SampleFiles.PngType, "pixel.png");

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "attachment.unsupported-type");
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory()
            .WithConfiguration($"{AttachmentsOptions.SectionName}:{nameof(AttachmentsOptions.MaxSizeBytes)}", MaxSizeBytes.ToString(CultureInfo.InvariantCulture))
            .WithConfiguration($"{AttachmentsOptions.SectionName}:{nameof(AttachmentsOptions.AllowedContentTypes)}", $"{SampleFiles.TextType}; {SampleFiles.PdfType}");

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
