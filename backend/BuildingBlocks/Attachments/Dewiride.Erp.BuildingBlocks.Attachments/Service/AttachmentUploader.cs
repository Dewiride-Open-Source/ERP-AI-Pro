using System.Buffers;
using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;
using Dewiride.Erp.BuildingBlocks.Attachments.Inspection;
using Dewiride.Erp.BuildingBlocks.Attachments.Persistence;
using Dewiride.Erp.BuildingBlocks.Attachments.Scanning;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage;
using Dewiride.Erp.BuildingBlocks.Attachments.Telemetry;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Service;

// The upload runs outside the application pipeline's unit of work: that would hold a SQL transaction open for the whole
// transfer and could replay the handler against a request body already read. Each database step is one SaveChanges instead,
// and the reservation written first guarantees a blob whose upload did not finish is found and removed later.
internal sealed partial class AttachmentUploader(
    AttachmentsDbContext context,
    IDocumentStore store,
    KeyRing keys,
    IActorContext actor,
    TimeProvider time,
    IOptionsMonitor<AttachmentsOptions> options,
    AttachmentsMetrics metrics,
    ILogger<AttachmentUploader> logger,
    IAttachmentScanner? scanner = null)
{
    private const int ReadSize = 64 * 1024;

    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(30);

    public async Task<Result<AttachmentDetails>> UploadAsync(AttachmentUpload upload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);

        var settings = options.CurrentValue;
        var fileName = FileNames.Sanitize(upload.FileName);
        if (fileName is null)
        {
            return Reject(AttachmentErrors.FileNameInvalid);
        }

        var contentType = ContentTypes.Normalize(upload.ContentType);
        if (!ContentTypes.TryParseAllowList(settings.AllowedContentTypes, out var allowed, out _) || !allowed.Contains(contentType))
        {
            return Reject(AttachmentErrors.UnsupportedType);
        }

        var head = new byte[ContentTypes.HeadLength];
        var headLength = await upload.Content.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        if (headLength == 0)
        {
            return Reject(AttachmentErrors.Empty);
        }

        if (!ContentTypes.Matches(contentType, head.AsSpan(0, headLength), isWholeFile: headLength < head.Length))
        {
            return Reject(AttachmentErrors.ContentMismatch);
        }

        var reservation = new UploadReservation(StoredContentId.Create(), time.GetUtcNow(), actor.ActorId);
        context.UploadReservations.Add(reservation);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var settled = false;
        var committed = false;
        try
        {
            await using var documentUpload = store.BeginUpload(reservation.ContentId);
            var written = await WriteEnvelopeAsync(new ReplayedHeadStream(head.AsMemory(0, headLength), upload.Content), documentUpload.Content, reservation.ContentId, settings.MaxSizeBytes, cancellationToken).ConfigureAwait(false);
            if (written.IsFailure)
            {
                return Reject(written.Error!);
            }

            var (sha256, length, keyId, scanStatus) = written.Value;
            var existing = await context.StoredContents
                .Where(c => c.Sha256 == sha256 && c.Length == length)
                .OrderBy(c => c.StoredAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            var content = existing;
            if (content is null)
            {
                await documentUpload.CommitAsync(cancellationToken).ConfigureAwait(false);
                committed = true;
                content = new StoredContent(reservation.ContentId, sha256, length, keyId, time.GetUtcNow());
                context.StoredContents.Add(content);
            }

            var attachment = Attachment.Create(content, fileName, contentType, scanStatus);
            context.Attachments.Add(attachment);
            context.UploadReservations.Remove(reservation);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            settled = true;

            metrics.UploadAccepted(length, deduplicated: existing is not null);
            LogStored(logger, attachment.Id.Value, content.Id.Value, length, content.KeyId, existing is not null);

            return new AttachmentDetails(attachment.Id, fileName, contentType, length, Convert.ToHexStringLower(sha256), scanStatus, attachment.CreatedAt, attachment.CreatedBy);
        }
        finally
        {
            if (!settled)
            {
                await AbandonAsync(reservation).ConfigureAwait(false);
            }
            else if (!committed)
            {
                await DiscardUncommittedAsync(reservation.ContentId).ConfigureAwait(false);
            }
        }
    }

    private async Task<Result<WrittenEnvelope>> WriteEnvelopeAsync(Stream source, Stream destination, StoredContentId contentId, long maxSizeBytes, CancellationToken cancellationToken)
    {
        var key = keys.Current;
        var dataKey = RandomNumberGenerator.GetBytes(EnvelopeHeader.DataKeySize);
        var buffer = ArrayPool<byte>.Shared.Rent(ReadSize);
        IAttachmentScan? scan = null;
        try
        {
            var header = EnvelopeHeader.Create(key, contentId, dataKey);
            using var writer = await EnvelopeWriter.StartAsync(destination, header, dataKey, cancellationToken).ConfigureAwait(false);
            CryptographicOperations.ZeroMemory(dataKey);
            scan = scanner is null ? null : await scanner.StartAsync(cancellationToken).ConfigureAwait(false);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long length = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, ReadSize), cancellationToken).ConfigureAwait(false)) > 0)
            {
                length += read;
                if (length > maxSizeBytes)
                {
                    return AttachmentErrors.TooLarge;
                }

                var chunk = buffer.AsMemory(0, read);
                hash.AppendData(chunk.Span);
                if (scan is not null)
                {
                    await scan.AppendAsync(chunk, cancellationToken).ConfigureAwait(false);
                }

                await writer.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
            }

            await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
            var scanStatus = AttachmentScanStatus.NotScanned;
            if (scan is not null)
            {
                if (await scan.CompleteAsync(cancellationToken).ConfigureAwait(false) == AttachmentScanVerdict.Infected)
                {
                    LogInfected(logger, contentId.Value);
                    return AttachmentErrors.Infected;
                }

                scanStatus = AttachmentScanStatus.Clean;
            }

            return new WrittenEnvelope(hash.GetHashAndReset(), length, key.Id, scanStatus);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            if (scan is not null)
            {
                await scan.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    // Runs on its own deadline rather than the request's, which is typically what was cancelled. A blob is deleted only when
    // no StoredContent row references it: a save that failed on the client may still have committed on the server.
    private async Task AbandonAsync(UploadReservation reservation)
    {
        using var deadline = new CancellationTokenSource(CleanupTimeout, time);
        try
        {
            context.ChangeTracker.Clear();
            if (!await context.StoredContents.AnyAsync(c => c.Id == reservation.ContentId, deadline.Token).ConfigureAwait(false))
            {
                await store.DeleteAsync(reservation.ContentId, deadline.Token).ConfigureAwait(false);
            }

            context.UploadReservations.Remove(reservation);
            await context.SaveChangesAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogCleanupDeferred(logger, reservation.ContentId.Value, exception.GetType().Name);
        }
    }

    private async Task DiscardUncommittedAsync(StoredContentId contentId)
    {
        using var deadline = new CancellationTokenSource(CleanupTimeout, time);
        try
        {
            await store.DeleteAsync(contentId, deadline.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogDiscardFailed(logger, contentId.Value, exception.GetType().Name);
        }
    }

    private Result<AttachmentDetails> Reject(Error error)
    {
        metrics.UploadRejected(error.Code);

        return error;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Stored attachment {AttachmentId} as content {ContentId} ({Length} bytes, key {KeyId}, deduplicated: {Deduplicated})")]
    private static partial void LogStored(ILogger logger, Guid attachmentId, Guid contentId, long length, string keyId, bool deduplicated);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The virus scanner rejected the upload staged as content {ContentId}")]
    private static partial void LogInfected(ILogger logger, Guid contentId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not clean up the abandoned upload of content {ContentId} ({ExceptionType}); the reservation sweeper will remove it")]
    private static partial void LogCleanupDeferred(ILogger logger, Guid contentId, string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not discard the uncommitted blocks of content {ContentId} ({ExceptionType}); Blob Storage removes them after seven days")]
    private static partial void LogDiscardFailed(ILogger logger, Guid contentId, string exceptionType);

    private readonly record struct WrittenEnvelope(byte[] Sha256, long Length, string KeyId, AttachmentScanStatus ScanStatus);
}
