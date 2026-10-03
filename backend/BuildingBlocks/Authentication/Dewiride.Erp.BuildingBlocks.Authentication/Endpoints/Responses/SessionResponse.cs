namespace Dewiride.Erp.BuildingBlocks.Authentication.Endpoints.Responses;

/// <summary>When the session of the signed-in person ends.</summary>
/// <param name="ExpiresAt">UTC time the session ends unless a request renews it first: the idle timeout after it was last renewed, never after <paramref name="LifetimeEndsAt"/>.</param>
/// <param name="LifetimeEndsAt">UTC time the session ends whatever happens, the session lifetime after the person signed in.</param>
internal sealed record SessionResponse(DateTimeOffset ExpiresAt, DateTimeOffset LifetimeEndsAt);
