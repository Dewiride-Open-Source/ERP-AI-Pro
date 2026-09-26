using System.Buffers.Text;
using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Service;

public sealed class AttachmentDownloadTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string NotFound = "attachment.not-found";

    private static readonly DateTimeOffset LinkCreatedAt = new(2026, 9, 26, 9, 30, 0, TimeSpan.Zero);

    private static readonly int TokenLength = Base64Url.GetEncodedLength(DownloadLink.TokenLength);

    public static TheoryData<string?> MalformedTokens { get; } = new() { (string?)null, string.Empty, "not-a-link", new string('A', TokenLength - 1), new string('A', TokenLength + 1) };

    private TimeSpan LinkLifetime => factory.Services.GetRequiredService<IOptions<AttachmentsOptions>>().Value.DownloadLinkLifetime;

    [Fact]
    public async Task OpenDownloadAsync_ValidLink_ReturnsTheOriginalFileAndRecordsOneRedemption()
    {
        var clock = new FakeTimeProvider(LinkCreatedAt);
        var fileName = $"{TestFiles.Stamp()} bank statement.pdf";
        var content = TestFiles.Pdf(150_000);
        var details = await factory.UploadAsync(fileName, TestFiles.PdfType, content);
        var link = await CreateLinkAsync(details.Id, clock);

        var opened = await OpenAsync(details.Id, link.Token, clock);

        Assert.Equal(content, opened.Value.Content);
        Assert.Equal(fileName, opened.Value.FileName);
        Assert.Equal(TestFiles.PdfType, opened.Value.ContentType);
        Assert.Equal(content.Length, opened.Value.SizeBytes);
        Assert.Equal(LinkCreatedAt + LinkLifetime, link.ExpiresAt);
        var stored = await factory.QueryAsync((context, cancellationToken) => context.DownloadLinks.AsNoTracking().SingleAsync(l => l.AttachmentId == details.Id, cancellationToken));
        Assert.Equal(SHA256.HashData(Base64Url.DecodeFromChars(link.Token)), stored.TokenHash);
        Assert.Equal(ActorIds.System, stored.ActorId);
        Assert.Equal(link.ExpiresAt, stored.ExpiresAt);
        var redemption = Assert.Single(await factory.QueryAsync((context, cancellationToken) =>
            context.DownloadRedemptions.AsNoTracking().Where(r => r.AttachmentId == details.Id).ToListAsync(cancellationToken)));
        Assert.Equal(stored.Id, redemption.LinkId);
        Assert.Equal(ActorIds.System, redemption.ActorId);
        Assert.Equal(LinkCreatedAt, redemption.RedeemedAt);
    }

    [Fact]
    public async Task OpenDownloadAsync_SameLinkTwice_RecordsASecondRedemption()
    {
        var details = await UploadAsync();
        var link = await CreateLinkAsync(details.Id);

        Assert.True((await OpenAsync(details.Id, link.Token)).IsSuccess);
        Assert.True((await OpenAsync(details.Id, link.Token)).IsSuccess);

        Assert.Equal(2, await RedemptionsAsync(details.Id));
    }

    [Fact]
    public async Task OpenDownloadAsync_TamperedToken_ReturnsNotFoundWithoutARedemption()
    {
        var details = await UploadAsync();
        var token = (await CreateLinkAsync(details.Id)).Token;
        var tampered = (token[0] == 'A' ? 'B' : 'A') + token[1..];

        var opened = await OpenAsync(details.Id, tampered);

        Assert.Equal(NotFound, opened.Error?.Code);
        Assert.Equal(0, await RedemptionsAsync(details.Id));
    }

    [Theory]
    [MemberData(nameof(MalformedTokens))]
    public async Task OpenDownloadAsync_MalformedToken_ReturnsNotFoundWithoutARedemption(string? token)
    {
        var details = await UploadAsync();
        await CreateLinkAsync(details.Id);

        var opened = await OpenAsync(details.Id, token);

        Assert.Equal(NotFound, opened.Error?.Code);
        Assert.Equal(0, await RedemptionsAsync(details.Id));
    }

    [Fact]
    public async Task OpenDownloadAsync_TokenOfTheRightLengthWithCharactersOutsideBase64Url_ReturnsNotFoundWithoutARedemption()
    {
        var details = await UploadAsync();
        await CreateLinkAsync(details.Id);

        var opened = await OpenAsync(details.Id, new string('!', TokenLength));

        Assert.Equal(NotFound, opened.Error?.Code);
        Assert.Equal(0, await RedemptionsAsync(details.Id));
    }

    [Fact]
    public async Task CreateDownloadLinkAsync_DeletedAttachment_ReturnsNotFound()
    {
        var details = await UploadAsync();
        await DeleteAsync(details.Id);

        await using var scope = factory.Services.CreateAsyncScope();
        var link = await scope.ServiceProvider.GetRequiredService<IAttachmentService>().CreateDownloadLinkAsync(details.Id, TestContext.Current.CancellationToken);

        Assert.Equal(NotFound, link.Error?.Code);
        Assert.False(await factory.QueryAsync((context, cancellationToken) => context.DownloadLinks.AnyAsync(l => l.AttachmentId == details.Id, cancellationToken)));
    }

    [Fact]
    public async Task OpenDownloadAsync_TokenOfAnotherAttachment_ReturnsNotFoundWithoutARedemption()
    {
        var requested = await UploadAsync();
        var other = await UploadAsync();
        var otherLink = await CreateLinkAsync(other.Id);

        var opened = await OpenAsync(requested.Id, otherLink.Token);

        Assert.Equal(NotFound, opened.Error?.Code);
        Assert.Equal(0, await RedemptionsAsync(requested.Id));
        Assert.Equal(0, await RedemptionsAsync(other.Id));
    }

    [Fact]
    public async Task OpenDownloadAsync_LinkOfAnotherPerson_ReturnsNotFoundWithoutARedemption()
    {
        var owner = new TestActorContext { ActorId = Guid.CreateVersion7() };
        var details = await UploadAsync();
        var link = await CreateLinkAsync(details.Id, owner);

        var opened = await OpenAsync(details.Id, link.Token, new TestActorContext { ActorId = Guid.CreateVersion7() });

        Assert.Equal(NotFound, opened.Error?.Code);
        Assert.Equal(0, await RedemptionsAsync(details.Id));
        Assert.True((await OpenAsync(details.Id, link.Token, owner)).IsSuccess);
    }

    [Fact]
    public async Task OpenDownloadAsync_AtTheInstantTheLinkExpires_ReturnsNotFoundWithoutARedemption()
    {
        var clock = new FakeTimeProvider(LinkCreatedAt);
        var details = await UploadAsync();
        var link = await CreateLinkAsync(details.Id, clock);
        clock.Advance(LinkLifetime);

        var opened = await OpenAsync(details.Id, link.Token, clock);

        Assert.Equal(NotFound, opened.Error?.Code);
        Assert.Equal(0, await RedemptionsAsync(details.Id));
    }

    [Fact]
    public async Task OpenDownloadAsync_OneTickBeforeTheLinkExpires_ServesTheFile()
    {
        var clock = new FakeTimeProvider(LinkCreatedAt);
        var details = await UploadAsync();
        var link = await CreateLinkAsync(details.Id, clock);
        clock.Advance(LinkLifetime - TimeSpan.FromTicks(1));

        var opened = await OpenAsync(details.Id, link.Token, clock);

        Assert.True(opened.IsSuccess);
        Assert.Equal(1, await RedemptionsAsync(details.Id));
    }

    [Fact]
    public async Task OpenDownloadAsync_DeletedAttachment_ReturnsNotFoundWithoutARedemption()
    {
        var details = await UploadAsync();
        var link = await CreateLinkAsync(details.Id);
        await DeleteAsync(details.Id);

        var opened = await OpenAsync(details.Id, link.Token);

        Assert.Equal(NotFound, opened.Error?.Code);
        Assert.Equal(0, await RedemptionsAsync(details.Id));
    }

    [Fact]
    public async Task OpenDownloadAsync_BlobMissing_ReturnsNotFoundWithoutARedemptionAndLogsTheMissingContent()
    {
        var logger = new FakeLogger<AttachmentService>();
        var details = await UploadAsync();
        var link = await CreateLinkAsync(details.Id);
        await StoredBlobs.DeleteAsync(await factory.ContentIdOfAsync(details.Id));

        var opened = await OpenAsync(details.Id, link.Token, logger);

        Assert.Equal(NotFound, opened.Error?.Code);
        Assert.Equal(0, await RedemptionsAsync(details.Id));
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.IsType<StoredContentMissingException>(record.Exception);
    }

    [Fact]
    public async Task OpenDownloadAsync_BlobWithOneCiphertextBitFlipped_ReturnsNotFoundWithoutARedemptionAndLogsTheRejection()
    {
        var logger = new FakeLogger<AttachmentService>();
        var details = await UploadAsync();
        var link = await CreateLinkAsync(details.Id);
        var contentId = await factory.ContentIdOfAsync(details.Id);
        var envelope = await StoredBlobs.ReadAsync(contentId);
        envelope[^(EnvelopeHeader.TagSize + 1)] ^= 0x01;
        await StoredBlobs.ReplaceAsync(contentId, envelope);

        var opened = await OpenAsync(details.Id, link.Token, logger);

        Assert.Equal(NotFound, opened.Error?.Code);
        Assert.Equal(0, await RedemptionsAsync(details.Id));
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.IsType<EnvelopeFormatException>(record.Exception);
    }

    [Fact]
    public async Task OpenDownloadAsync_BlobReplacedByTheEnvelopeOfAnotherFile_ReturnsNotFoundWithoutARedemption()
    {
        var logger = new FakeLogger<AttachmentService>();
        var details = await UploadAsync();
        var other = await UploadAsync();
        var link = await CreateLinkAsync(details.Id);
        await StoredBlobs.ReplaceAsync(await factory.ContentIdOfAsync(details.Id), await StoredBlobs.ReadAsync(await factory.ContentIdOfAsync(other.Id)));

        var opened = await OpenAsync(details.Id, link.Token, logger);

        Assert.Equal(NotFound, opened.Error?.Code);
        Assert.Equal(0, await RedemptionsAsync(details.Id));
        Assert.IsType<EnvelopeFormatException>(Assert.Single(logger.Collector.GetSnapshot()).Exception);
    }

    private Task<AttachmentDetails> UploadAsync() =>
        factory.UploadAsync($"{TestFiles.Stamp()}-receipt.pdf", TestFiles.PdfType, TestFiles.Pdf(5_000));

    private async Task<DownloadLinkDetails> CreateLinkAsync(AttachmentId id, params object[] serviceArguments)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = ActivatorUtilities.CreateInstance<AttachmentService>(scope.ServiceProvider, serviceArguments);

        return (await service.CreateDownloadLinkAsync(id, TestContext.Current.CancellationToken)).Value;
    }

    private async Task<Result<DownloadedFile>> OpenAsync(AttachmentId id, string? token, params object[] serviceArguments)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = ActivatorUtilities.CreateInstance<AttachmentService>(scope.ServiceProvider, serviceArguments);
        var opened = await service.OpenDownloadAsync(id, token, TestContext.Current.CancellationToken);
        if (opened.IsFailure)
        {
            return opened.Error!;
        }

        await using var download = opened.Value;
        using var buffer = new MemoryStream();
        await download.Content.CopyToAsync(buffer, TestContext.Current.CancellationToken);

        return new DownloadedFile(buffer.ToArray(), download.FileName, download.ContentType, download.SizeBytes);
    }

    private async Task DeleteAsync(AttachmentId id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var deleted = await scope.ServiceProvider.GetRequiredService<IAttachmentService>().DeleteAsync(id, TestContext.Current.CancellationToken);

        Assert.True(deleted.IsSuccess);
    }

    private Task<int> RedemptionsAsync(AttachmentId id) =>
        factory.QueryAsync((context, cancellationToken) => context.DownloadRedemptions.CountAsync(r => r.AttachmentId == id, cancellationToken));

    private sealed record DownloadedFile(byte[] Content, string FileName, string ContentType, long SizeBytes);
}
