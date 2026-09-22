using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Actors;

internal sealed class HttpActorContext : IActorContext
{
    public const string ObjectIdentifierClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    public const string ShortObjectIdentifierClaim = "oid";

    private readonly Lazy<(Guid ActorId, bool IsAuthenticated)> _actor;

    public HttpActorContext(IHttpContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        _actor = new Lazy<(Guid, bool)>(() => Resolve(accessor.HttpContext?.User));
    }

    public Guid ActorId => _actor.Value.ActorId;

    public bool IsAuthenticated => _actor.Value.IsAuthenticated;

    private static (Guid ActorId, bool IsAuthenticated) Resolve(ClaimsPrincipal? user)
    {
        if (user is null)
        {
            return (ActorIds.System, false);
        }

        if (user.Identity?.IsAuthenticated != true)
        {
            return (ActorIds.Anonymous, false);
        }

        var value = user.FindFirstValue(ObjectIdentifierClaim) ?? user.FindFirstValue(ShortObjectIdentifierClaim);
        return Guid.TryParse(value, out var objectId)
            ? (objectId, true)
            : throw new InvalidOperationException("The authenticated principal carries no object identifier claim; every ERP sign-in is an Entra identity with an oid.");
    }
}
