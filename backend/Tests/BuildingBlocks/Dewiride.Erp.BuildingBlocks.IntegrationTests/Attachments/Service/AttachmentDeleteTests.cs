using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Service;

public sealed class AttachmentDeleteTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task DeleteAsync_StoredAttachment_HidesItButKeepsTheRowAndTheBlob()
    {
        var stamp = TestFiles.Stamp();
        var details = await factory.UploadAsync($"{stamp}-offer-letter.txt", TestFiles.TextType, TestFiles.Text(stamp, 2_000));

        var deleted = await UseServiceAsync(service => service.DeleteAsync(details.Id, TestContext.Current.CancellationToken));

        Assert.True(deleted.IsSuccess);
        Assert.Equal("attachment.not-found", (await UseServiceAsync(service => service.GetAsync(details.Id, TestContext.Current.CancellationToken))).Error?.Code);
        var listed = await UseServiceAsync(service => service.ListAsync(ListRequest.Parse(null, null, null, $"fileName:contains:{stamp}").Value, TestContext.Current.CancellationToken));
        Assert.Empty(listed.Value.Items);
        var row = await factory.QueryAsync((context, cancellationToken) => context.Attachments.IncludeDeleted().AsNoTracking().SingleAsync(a => a.Id == details.Id, cancellationToken));
        Assert.True(row.IsDeleted);
        Assert.NotNull(row.DeletedAt);
        Assert.Equal(ActorIds.System, row.DeletedBy);
        Assert.True(await factory.HasStoredContentAsync(row.ContentId));
        Assert.True(await StoredBlobs.IsCommittedAsync(row.ContentId));
    }

    [Fact]
    public async Task DeleteAsync_AttachmentAlreadyDeleted_ReturnsNotFound()
    {
        var stamp = TestFiles.Stamp();
        var details = await factory.UploadAsync($"{stamp}-draft.txt", TestFiles.TextType, TestFiles.Text(stamp, 2_000));
        Assert.True((await UseServiceAsync(service => service.DeleteAsync(details.Id, TestContext.Current.CancellationToken))).IsSuccess);

        var again = await UseServiceAsync(service => service.DeleteAsync(details.Id, TestContext.Current.CancellationToken));

        Assert.Equal("attachment.not-found", again.Error?.Code);
    }

    [Fact]
    public async Task DeleteAsync_UnknownAttachment_ReturnsNotFound()
    {
        var result = await UseServiceAsync(service => service.DeleteAsync(AttachmentId.Create(), TestContext.Current.CancellationToken));

        Assert.Equal("attachment.not-found", result.Error?.Code);
    }

    private async Task<T> UseServiceAsync<T>(Func<IAttachmentService, Task<T>> call)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await call(scope.ServiceProvider.GetRequiredService<IAttachmentService>());
    }
}
