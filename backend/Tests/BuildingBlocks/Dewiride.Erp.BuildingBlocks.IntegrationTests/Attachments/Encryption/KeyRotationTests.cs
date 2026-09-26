using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Encryption;

public sealed class KeyRotationTests
{
    private const string RetiredKeysKey = $"{AttachmentsOptions.SectionName}:RetiredEncryptionKeys";

    [Fact]
    public async Task OpenDownloadAsync_ContentStoredUnderARetiredKey_IsStillServed()
    {
        var previous = NewKey("previous");
        var content = TestFiles.Pdf(100_000);
        var details = await UploadUnderAsync(previous, content);

        await using var rotated = new ErpApiFactory()
            .WithConfiguration(ErpApiFactory.AttachmentsEncryptionKeyKey, NewKey("current"))
            .WithConfiguration(RetiredKeysKey, previous);

        Assert.Equal(content, await rotated.DownloadAsync(details.Id));
        Assert.Equal("previous", await KeyIdOfAsync(rotated, details.Id));
    }

    [Fact]
    public async Task UploadAsync_AfterRotation_StoresNewContentUnderTheCurrentKey()
    {
        await using var rotated = new ErpApiFactory()
            .WithConfiguration(ErpApiFactory.AttachmentsEncryptionKeyKey, NewKey("current"))
            .WithConfiguration(RetiredKeysKey, NewKey("previous"));

        var details = await rotated.UploadAsync($"{TestFiles.Stamp()}-ledger.pdf", TestFiles.PdfType, TestFiles.Pdf(10_000));

        Assert.Equal("current", await KeyIdOfAsync(rotated, details.Id));
    }

    [Fact]
    public async Task OpenDownloadAsync_ContentStoredUnderAKeyNoLongerConfigured_ReturnsNotFound()
    {
        var details = await UploadUnderAsync(NewKey("forgotten"), TestFiles.Pdf(10_000));
        await using var rotated = new ErpApiFactory().WithConfiguration(ErpApiFactory.AttachmentsEncryptionKeyKey, NewKey("current"));
        await using var scope = rotated.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
        var link = await service.CreateDownloadLinkAsync(details.Id, TestContext.Current.CancellationToken);

        var opened = await service.OpenDownloadAsync(details.Id, link.Value.Token, TestContext.Current.CancellationToken);

        Assert.Equal("attachment.not-found", opened.Error?.Code);
    }

    private static string NewKey(string id) => $"{id}:{Convert.ToBase64String(RandomNumberGenerator.GetBytes(EncryptionKey.MaterialLength))}";

    private static async Task<AttachmentDetails> UploadUnderAsync(string key, byte[] content)
    {
        await using var factory = new ErpApiFactory().WithConfiguration(ErpApiFactory.AttachmentsEncryptionKeyKey, key);

        return await factory.UploadAsync($"{TestFiles.Stamp()}-ledger.pdf", TestFiles.PdfType, content);
    }

    private static Task<string> KeyIdOfAsync(ErpApiFactory factory, AttachmentId id) =>
        factory.QueryAsync((context, cancellationToken) => context.Attachments.Where(a => a.Id == id).Select(a => a.Content.KeyId).SingleAsync(cancellationToken));
}
