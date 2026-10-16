using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Service;

public sealed class AttachmentChangeRecordsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string FilesKey = "platform.attachments.files";

    [Fact]
    public async Task UploadAsync_StoredFile_RecordsItsCreationWithItsValuesNamedByItsFileName()
    {
        var stamp = TestFiles.Stamp();
        var fileName = $"{stamp}-payslip.txt";
        var details = await factory.UploadAsync(fileName, TestFiles.TextType, TestFiles.Text(stamp, 2_000));

        var record = Assert.Single(await HistoryOfAsync(details.Id.Value));

        Assert.Equal(ChangeOperation.Created, record.Operation);
        Assert.Equal((FilesKey, details.Id.Value, FilesKey, details.Id.Value), (record.EntityType, record.EntityId, record.SubjectType, record.SubjectId));
        Assert.Equal(fileName, record.SubjectName);
        Assert.Equal(details.CreatedAt, record.OccurredAt);
        Assert.Equal((ChangeActorKind.System, details.CreatedBy), (record.ActorKind, record.ActorId));
        var values = record.Changes.ToDictionary(value => value.Property);
        Assert.Equal(["contentId", "contentType", "fileName", "scanStatus"], values.Keys.Order(StringComparer.Ordinal));
        Assert.Equal((ChangeValueKind.Text, fileName), (values["fileName"].Kind, values["fileName"].After));
        Assert.Equal((ChangeValueKind.Text, TestFiles.TextType), (values["contentType"].Kind, values["contentType"].After));
        Assert.Equal((ChangeValueKind.Enum, "notScanned"), (values["scanStatus"].Kind, values["scanStatus"].After));
        Assert.Equal(ChangeValueKind.Id, values["contentId"].Kind);
        Assert.True(Guid.TryParse(values["contentId"].After, out _));
        Assert.All(values.Values, value => Assert.Null(value.Before));
    }

    [Fact]
    public async Task DeleteAsync_StoredFile_RecordsItsDeletionStillNamedByItsFileName()
    {
        var stamp = TestFiles.Stamp();
        var fileName = $"{stamp}-draft-offer.txt";
        var details = await factory.UploadAsync(fileName, TestFiles.TextType, TestFiles.Text(stamp, 2_000));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<IAttachmentService>().DeleteAsync(details.Id, TestContext.Current.CancellationToken)).IsSuccess);
        }

        var history = await HistoryOfAsync(details.Id.Value);

        Assert.Equal([ChangeOperation.Deleted, ChangeOperation.Created], history.Select(record => record.Operation));
        var deletion = history[0];
        Assert.Equal(fileName, deletion.SubjectName);
        Assert.Empty(deletion.Changes);
    }

    private async Task<IReadOnlyList<ChangeRecordDetails>> HistoryOfAsync(Guid attachmentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var listed = await scope.ServiceProvider.GetRequiredService<IChangeHistory>()
            .ListAsync(ListRequest.Parse(null, null, null, $"subjectId:eq:{attachmentId}").Value, TestContext.Current.CancellationToken);

        return listed.Value.Items;
    }
}
