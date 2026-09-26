using System.Buffers.Text;
using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;
using Dewiride.Erp.BuildingBlocks.Attachments.Inspection;
using Dewiride.Erp.BuildingBlocks.Attachments.Persistence;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage;
using Dewiride.Erp.BuildingBlocks.Attachments.Telemetry;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Service;

internal sealed partial class AttachmentService(
    AttachmentsDbContext context,
    AttachmentUploader uploader,
    IDocumentStore store,
    KeyRing keys,
    IActorContext actor,
    TimeProvider time,
    IOptionsMonitor<AttachmentsOptions> options,
    AttachmentsMetrics metrics,
    ILogger<AttachmentService> logger) : IAttachmentService
{
    private static readonly SortableFields<Attachment> Sortable = new SortableFields<Attachment>()
        .Add("fileName", a => a.FileName)
        .Add("contentType", a => a.ContentType)
        .Add("sizeBytes", a => a.Content.Length)
        .Add("createdAt", a => a.CreatedAt);

    private static readonly FilterableFields<Attachment> Filterable = new FilterableFields<Attachment>()
        .Add("fileName", a => a.FileName)
        .Add("contentType", a => a.ContentType)
        .Add("createdAt", a => a.CreatedAt);

    private static readonly SortRequest NewestFirst = new([new SortTerm("createdAt", SortDirection.Descending)]);

    public UploadPolicy GetUploadPolicy()
    {
        var settings = options.CurrentValue;
        var allowed = ContentTypes.TryParseAllowList(settings.AllowedContentTypes, out var types, out _) ? types.Order(StringComparer.Ordinal).ToArray() : [];

        return new UploadPolicy(settings.MaxSizeBytes, allowed);
    }

    public Task<Result<AttachmentDetails>> UploadAsync(AttachmentUpload upload, CancellationToken cancellationToken) =>
        uploader.UploadAsync(upload, cancellationToken);

    public async Task<Result<AttachmentDetails>> GetAsync(AttachmentId id, CancellationToken cancellationToken)
    {
        var attachment = await context.Attachments
            .AsNoTracking()
            .Include(a => a.Content)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return attachment is null ? AttachmentErrors.NotFound : Details(attachment);
    }

    public async Task<Result<PagedResult<AttachmentDetails>>> ListAsync(ListRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sort = (request.Sort.Terms.Count == 0 ? NewestFirst : request.Sort).Resolve(Sortable);
        if (sort.IsFailure)
        {
            return sort.Error!;
        }

        var filter = request.Filter.Resolve(Filterable);
        if (filter.IsFailure)
        {
            return filter.Error!;
        }

        var page = await context.Attachments
            .AsNoTracking()
            .Include(a => a.Content)
            .ApplyFilter(filter.Value)
            .ApplySort(sort.Value, a => a.Id)
            .ToPagedResultAsync(request.Page, cancellationToken)
            .ConfigureAwait(false);

        return page.Map(Details);
    }

    public async Task<Result> DeleteAsync(AttachmentId id, CancellationToken cancellationToken)
    {
        var attachment = await context.Attachments.SingleOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);
        if (attachment is null)
        {
            return AttachmentErrors.NotFound;
        }

        context.Attachments.Remove(attachment);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<DownloadLinkDetails>> CreateDownloadLinkAsync(AttachmentId id, CancellationToken cancellationToken)
    {
        if (!await context.Attachments.AnyAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false))
        {
            return AttachmentErrors.NotFound;
        }

        var token = RandomNumberGenerator.GetBytes(DownloadLink.TokenLength);
        var createdAt = time.GetUtcNow();
        var expiresAt = createdAt + options.CurrentValue.DownloadLinkLifetime;
        context.DownloadLinks.Add(new DownloadLink(DownloadLinkId.Create(), id, actor.ActorId, SHA256.HashData(token), createdAt, expiresAt));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new DownloadLinkDetails(Base64Url.EncodeToString(token), expiresAt);
    }

    // Every refusal answers the same not-found error, so a caller cannot tell a wrong token from an expired link, another
    // person's link or a deleted attachment.
    public async Task<Result<AttachmentDownload>> OpenDownloadAsync(AttachmentId id, string? token, CancellationToken cancellationToken)
    {
        if (!TryHashToken(token, out var tokenHash))
        {
            return Refuse();
        }

        var now = time.GetUtcNow();
        var actorId = actor.ActorId;
        var match = await (
                from link in context.DownloadLinks
                join attachment in context.Attachments on link.AttachmentId equals attachment.Id
                where link.TokenHash == tokenHash && link.AttachmentId == id && link.ActorId == actorId && link.ExpiresAt > now
                select new { LinkId = link.Id, attachment.FileName, attachment.ContentType, attachment.ContentId, attachment.Content.Length })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (match is null)
        {
            return Refuse();
        }

        Stream content;
        try
        {
            content = await OpenContentAsync(match.ContentId, match.Length, cancellationToken).ConfigureAwait(false);
        }
        catch (StoredContentMissingException exception)
        {
            LogContentMissing(logger, exception, match.ContentId.Value, id.Value);
            return Refuse();
        }
        catch (EnvelopeFormatException exception)
        {
            LogEnvelopeRejected(logger, exception, match.ContentId.Value, id.Value);
            return Refuse();
        }

        try
        {
            context.DownloadRedemptions.Add(new DownloadRedemption(DownloadRedemptionId.Create(), match.LinkId, id, actorId, now));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await content.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        metrics.DownloadServed();

        return new AttachmentDownload(content, match.FileName, match.ContentType, match.Length);
    }

    private static AttachmentDetails Details(Attachment attachment) =>
        new(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.Content.Length,
            Convert.ToHexStringLower(attachment.Content.Sha256),
            attachment.ScanStatus,
            attachment.CreatedAt,
            attachment.CreatedBy);

    private static bool TryHashToken(string? token, out byte[] tokenHash)
    {
        tokenHash = [];

        // TryDecodeFromChars throws rather than returning false for a character outside the alphabet, so the text is checked first.
        if (token is null || !Base64Url.IsValid(token, out var decodedLength) || decodedLength != DownloadLink.TokenLength)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[DownloadLink.TokenLength];
        if (!Base64Url.TryDecodeFromChars(token, bytes, out var written) || written != DownloadLink.TokenLength)
        {
            return false;
        }

        tokenHash = SHA256.HashData(bytes);
        return true;
    }

    private async Task<Stream> OpenContentAsync(StoredContentId contentId, long length, CancellationToken cancellationToken)
    {
        var envelope = await store.OpenReadAsync(contentId, cancellationToken).ConfigureAwait(false);
        try
        {
            return await EnvelopeReadStream.OpenAsync(envelope, keys, contentId, length, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await envelope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private Result<AttachmentDownload> Refuse()
    {
        metrics.DownloadRefused();

        return AttachmentErrors.NotFound;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Stored content {ContentId} of attachment {AttachmentId} has no blob")]
    private static partial void LogContentMissing(ILogger logger, Exception exception, Guid contentId, Guid attachmentId);

    [LoggerMessage(Level = LogLevel.Error, Message = "The blob of stored content {ContentId} of attachment {AttachmentId} failed envelope authentication and was not served")]
    private static partial void LogEnvelopeRejected(ILogger logger, Exception exception, Guid contentId, Guid attachmentId);
}
