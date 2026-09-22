using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Idempotency.Storage;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Idempotency.Http;

internal sealed class IdempotencyMiddleware(RequestDelegate next, TimeProvider timeProvider, IOptions<IdempotencyOptions> options)
{
    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<RequireIdempotencyKeyMetadata>() is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var parsed = IdempotencyKeyHeader.Parse(context.Request.Headers);
        switch (parsed.State)
        {
            case KeyParseState.Missing:
                await IdempotencyProblems.WriteAsync(context, problemDetails, StatusCodes.Status400BadRequest, IdempotencyProblems.KeyMissing, $"This request needs an '{IdempotencyKeyHeader.Name}' header carrying a UUID.").ConfigureAwait(false);
                return;
            case KeyParseState.Invalid:
                await IdempotencyProblems.WriteAsync(context, problemDetails, StatusCodes.Status400BadRequest, IdempotencyProblems.KeyInvalid, $"The '{IdempotencyKeyHeader.Name}' header must carry exactly one UUID such as \"8e03978e-40d5-43e8-bc93-6894a57f9324\".").ConfigureAwait(false);
                return;
        }

        var cancellationToken = context.RequestAborted;
        var actorId = context.RequestServices.GetRequiredService<IActorContext>().ActorId;
        var store = context.RequestServices.GetRequiredService<IIdempotencyStore>();
        var fingerprint = await RequestFingerprint.ComputeAsync(context.Request, actorId, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var outcome = await store.BeginAsync(actorId, parsed.Key, fingerprint, now, now + options.Value.RetentionPeriod, cancellationToken).ConfigureAwait(false);
        switch (outcome.State)
        {
            case BeginState.InProgress:
                await IdempotencyProblems.WriteAsync(context, problemDetails, StatusCodes.Status409Conflict, IdempotencyProblems.InProgress, "A request with this idempotency key is still being processed; retry once it has finished.").ConfigureAwait(false);
                return;
            case BeginState.FingerprintMismatch:
                await IdempotencyProblems.WriteAsync(context, problemDetails, StatusCodes.Status422UnprocessableEntity, IdempotencyProblems.KeyReused, "This idempotency key was already used for a different request; use a new key for a new request.").ConfigureAwait(false);
                return;
            case BeginState.Completed:
                await ReplayAsync(context, problemDetails, outcome.Existing!, cancellationToken).ConfigureAwait(false);
                return;
        }

        await CaptureAsync(context, store, actorId, parsed.Key).ConfigureAwait(false);
    }

    private static async Task ReplayAsync(HttpContext context, IProblemDetailsService problemDetails, IdempotencyRecord record, CancellationToken cancellationToken)
    {
        if (record.Body is null)
        {
            await IdempotencyProblems.WriteAsync(context, problemDetails, StatusCodes.Status409Conflict, IdempotencyProblems.ReplayUnavailable, "The original response was too large to store, so it cannot be replayed; the request itself has already been processed.").ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = record.StatusCode!.Value;
        context.Response.Headers[IdempotencyKeyHeader.ReplayedName] = "true";
        if (record.ContentType is not null)
        {
            context.Response.ContentType = record.ContentType;
        }

        if (record.Location is not null)
        {
            context.Response.Headers.Location = record.Location;
        }

        context.Response.ContentLength = record.Body.Length;
        await context.Response.Body.WriteAsync(record.Body, cancellationToken).ConfigureAwait(false);
    }

    private async Task CaptureAsync(HttpContext context, IIdempotencyStore store, Guid actorId, Guid key)
    {
        var signal = context.RequestServices.GetRequiredService<UnitOfWorkSignal>();
        var original = context.Response.Body;
        await using var capture = new BoundedCaptureStream(original, options.Value.MaxStoredResponseBytes);
        context.Response.Body = capture;
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch
        {
            await ReleaseAsync(store, signal, actorId, key).ConfigureAwait(false);
            throw;
        }
        finally
        {
            context.Response.Body = original;
        }

        if (context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            await ReleaseAsync(store, signal, actorId, key).ConfigureAwait(false);
            return;
        }

        await store.CompleteAsync(actorId, key, new StoredResponse(context.Response.StatusCode, context.Response.ContentType, context.Response.Headers.Location, capture.Captured), timeProvider.GetUtcNow(), CancellationToken.None).ConfigureAwait(false);
    }

    // A committed unit of work must not be repeated, so a key whose command already wrote keeps its claim and answers 409 until it expires.
    private static Task ReleaseAsync(IIdempotencyStore store, UnitOfWorkSignal signal, Guid actorId, Guid key) =>
        signal.Committed ? Task.CompletedTask : store.AbandonAsync(actorId, key, CancellationToken.None);
}
