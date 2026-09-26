using System.Globalization;
using System.Net;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Uploads;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints.Uploads;

public sealed class UploadBodyLimitTests(UploadBodyLimitTests.Fixture fixture) : IClassFixture<UploadBodyLimitTests.Fixture>
{
    private const int HostBodyLimit = 1024;

    private const int MaxSizeBytes = 8 * 1024;

    [Fact]
    public async Task Post_FileOverTheHostBodyLimitWithinTheAttachmentLimit_IsStored()
    {
        using var client = fixture.Factory.CreateClient();
        var text = SampleFiles.Text(AttachmentsApi.UniqueToken().PadRight(MaxSizeBytes, 'x'));

        var attachment = await AttachmentsApi.UploadAsync(client, text, SampleFiles.TextType, "over-the-host-limit.txt");

        Assert.Equal(MaxSizeBytes, attachment.SizeBytes);
    }

    [Fact]
    public async Task Post_BodyOverTheAttachmentLimitAndItsMultipartAllowance_AnswersRequestTooLarge()
    {
        using var client = fixture.Factory.CreateClient();
        var text = SampleFiles.Text(AttachmentsApi.UniqueToken().PadRight((int)(MaxSizeBytes + UploadSizeLimit.MultipartAllowanceBytes), 'x'));

        using var response = await AttachmentsApi.PostFileAsync(client, text, SampleFiles.TextType, "over-the-body-limit.txt");

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge, "request.too-large");
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory()
            .WithConfiguration($"{ErpHostOptions.SectionName}:{nameof(ErpHostOptions.MaxRequestBodyBytes)}", HostBodyLimit.ToString(CultureInfo.InvariantCulture))
            .WithConfiguration($"{AttachmentsOptions.SectionName}:{nameof(AttachmentsOptions.MaxSizeBytes)}", MaxSizeBytes.ToString(CultureInfo.InvariantCulture))
            .WithKestrel();

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
