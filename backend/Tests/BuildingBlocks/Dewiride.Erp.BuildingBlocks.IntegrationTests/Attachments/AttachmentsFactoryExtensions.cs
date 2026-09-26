using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Persistence;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments;

internal static class AttachmentsFactoryExtensions
{
    public static async Task<AttachmentDetails> UploadAsync(this ErpApiFactory factory, string fileName, string contentType, byte[] content)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var stream = new MemoryStream(content, writable: false);
        var uploaded = await scope.ServiceProvider.GetRequiredService<IAttachmentService>()
            .UploadAsync(new AttachmentUpload(fileName, contentType, stream), TestContext.Current.CancellationToken);

        return uploaded.Value;
    }

    public static async Task<Result<AttachmentDetails>> UploadWithAsync(this ErpApiFactory factory, string? fileName, string? contentType, byte[] content, params object[] uploaderArguments)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var uploader = ActivatorUtilities.CreateInstance<AttachmentUploader>(scope.ServiceProvider, uploaderArguments);
        using var stream = new MemoryStream(content, writable: false);

        return await uploader.UploadAsync(new AttachmentUpload(fileName, contentType, stream), TestContext.Current.CancellationToken);
    }

    public static async Task<byte[]> DownloadAsync(this ErpApiFactory factory, AttachmentId id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
        var link = await service.CreateDownloadLinkAsync(id, TestContext.Current.CancellationToken);
        var opened = await service.OpenDownloadAsync(id, link.Value.Token, TestContext.Current.CancellationToken);
        await using var download = opened.Value;
        using var buffer = new MemoryStream();
        await download.Content.CopyToAsync(buffer, TestContext.Current.CancellationToken);

        return buffer.ToArray();
    }

    public static async Task<T> QueryAsync<T>(this ErpApiFactory factory, Func<AttachmentsDbContext, CancellationToken, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await query(scope.ServiceProvider.GetRequiredService<AttachmentsDbContext>(), TestContext.Current.CancellationToken);
    }

    public static Task<StoredContentId> ContentIdOfAsync(this ErpApiFactory factory, AttachmentId id) =>
        factory.QueryAsync((context, cancellationToken) => context.Attachments.IncludeDeleted().Where(a => a.Id == id).Select(a => a.ContentId).SingleAsync(cancellationToken));

    public static Task<bool> HasReservationsOfAsync(this ErpApiFactory factory, Guid actorId) =>
        factory.QueryAsync((context, cancellationToken) => context.UploadReservations.AnyAsync(r => r.ReservedBy == actorId, cancellationToken));

    public static Task<bool> HasStoredContentAsync(this ErpApiFactory factory, StoredContentId contentId) =>
        factory.QueryAsync((context, cancellationToken) => context.StoredContents.AnyAsync(c => c.Id == contentId, cancellationToken));
}
