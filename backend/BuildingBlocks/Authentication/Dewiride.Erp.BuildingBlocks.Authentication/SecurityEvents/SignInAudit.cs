using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;

namespace Dewiride.Erp.BuildingBlocks.Authentication.SecurityEvents;

// Every record carries the client address forwarded headers resolved and the request's correlation id. It is written whatever
// happens to the request meanwhile, like the session records, so a browser that leaves before the answer still leaves it.
// RecordAsync throws when the record cannot be written, for the events the request must not complete without; TryRecordAsync
// logs that failure and lets the request go on, and writes an event that names no actor only within
// AnonymousSecurityEventBudget, because anyone can cause one.
internal static partial class SignInAudit
{
    public static Task RecordAsync(HttpContext context, SecurityEventKind kind, string? detail = null, Guid? actor = null, Guid? clientApplication = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.RequestServices.GetRequiredService<ISecurityEventRecorder>()
            .RecordAsync(new SecurityEventEntry(kind, detail, actor, clientApplication, ClientAddressOf(context), CorrelationIdOf(context)), CancellationToken.None);
    }

    public static async Task TryRecordAsync(HttpContext context, SecurityEventKind kind, string? detail = null, Guid? actor = null, Guid? clientApplication = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (actor is null && !context.RequestServices.GetRequiredService<AnonymousSecurityEventBudget>().TryTake())
        {
            return;
        }

        try
        {
            await RecordAsync(context, kind, detail, actor, clientApplication).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogNotRecorded(context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SignInAudit).FullName!), exception, kind);
        }
    }

    public static Guid? ObjectIdOf(ClaimsPrincipal? principal) =>
        Guid.TryParse(principal?.FindFirstValue(ClaimConstants.Oid), out var objectId) ? objectId : null;

    private static string? ClientAddressOf(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return null;
        }

        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

    private static string? CorrelationIdOf(HttpContext context) =>
        context.Features.Get<ICorrelationIdFeature>()?.CorrelationId
        ?? (context.TraceIdentifier.Length <= CorrelationId.MaxLength ? context.TraceIdentifier : null);

    [LoggerMessage(Level = LogLevel.Error, Message = "A security event of kind {Kind} could not be recorded")]
    private static partial void LogNotRecorded(ILogger logger, Exception exception, SecurityEventKind kind);
}
