using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Origins;

// The client address is the one the forwarded headers resolved, in the family the client sent it in, so an IPv4 client reached
// over an IPv6 socket reads as IPv4. The correlation id is the request's, or its trace identifier when that fits the 64
// characters an audit row keeps.
public static class RequestOrigins
{
    public static string? ClientAddressOf(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return null;
        }

        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

    public static string? CorrelationIdOf(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Features.Get<ICorrelationIdFeature>()?.CorrelationId
            ?? (context.TraceIdentifier.Length <= CorrelationId.MaxLength ? context.TraceIdentifier : null);
    }
}
