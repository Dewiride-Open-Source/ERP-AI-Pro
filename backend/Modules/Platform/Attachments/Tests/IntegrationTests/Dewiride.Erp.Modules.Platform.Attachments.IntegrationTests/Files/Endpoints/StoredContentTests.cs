using System.Data;
using System.Net;
using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Blob;
using Dewiride.Erp.Testing.Sql;
using Microsoft.Data.SqlClient;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints;

public sealed class StoredContentTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task Get_ContentWhoseStoredBytesWereAltered_AnswersNotFound()
    {
        using var client = factory.CreateClient();
        var attachment = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"altered {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, "altered.txt");
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);
        var blob = await BlobOfAsync(attachment.Id);
        var envelope = (await blob.DownloadContentAsync(TestContext.Current.CancellationToken)).Value.Content.ToArray();
        envelope[^1] ^= 0x01;
        await blob.UploadAsync(BinaryData.FromBytes(envelope), overwrite: true, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, AttachmentErrors.NotFound.Code);
    }

    [Fact]
    public async Task Get_ContentWhoseStoredBytesAreMissing_AnswersNotFound()
    {
        using var client = factory.CreateClient();
        var attachment = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"missing {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, "missing.txt");
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);
        var blob = await BlobOfAsync(attachment.Id);
        await blob.DeleteAsync(cancellationToken: TestContext.Current.CancellationToken);

        using var response = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, AttachmentErrors.NotFound.Code);
    }

    [Fact]
    public async Task Get_ContentWhoseStoredBytesAreAnotherFilesEnvelope_AnswersNotFound()
    {
        using var client = factory.CreateClient();
        var target = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"target {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, "target.txt");
        var other = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"other {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, "other.txt");
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, target.Id);
        var otherEnvelope = (await (await BlobOfAsync(other.Id)).DownloadContentAsync(TestContext.Current.CancellationToken)).Value.Content;
        await (await BlobOfAsync(target.Id)).UploadAsync(otherEnvelope, overwrite: true, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, AttachmentErrors.NotFound.Code);
    }

    [Fact]
    public async Task Get_ContentOfAnUploadMatchingBytesStoredUnderAKeyNoLongerConfigured_StreamsTheFile()
    {
        var token = AttachmentsApi.UniqueToken();
        var text = SampleFiles.Text($"stored under a removed key {token}");
        using (var removed = new ErpApiFactory().WithConfiguration(ErpApiFactory.AttachmentsEncryptionKeyKey, EncryptionKey($"removed-{token}")))
        {
            using var removedClient = removed.CreateClient();
            await AttachmentsApi.UploadAsync(removedClient, text, SampleFiles.TextType, "under-the-removed-key.txt");
        }

        using var current = new ErpApiFactory().WithConfiguration(ErpApiFactory.AttachmentsEncryptionKeyKey, EncryptionKey($"current-{token}"));
        using var client = current.CreateClient();
        var attachment = await AttachmentsApi.UploadAsync(client, text, SampleFiles.TextType, "under-the-current-key.txt");
        var link = await AttachmentsApi.CreateDownloadLinkAsync(client, attachment.Id);

        using var response = await client.GetAsync(new Uri(link.Url, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(text, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    private static string EncryptionKey(string id) => $"{id}:{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}";

    private static async Task<BlobClient> BlobOfAsync(Guid attachmentId)
    {
        await using var connection = new SqlConnection(SqlTestDatabase.Current.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand("SELECT ContentId FROM files.Attachments WHERE Id = @id", connection);
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = attachmentId });
        var contentId = Assert.IsType<Guid>(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        return BlobTestContainer.Current.Container.GetBlobClient(contentId.ToString("N"));
    }
}
