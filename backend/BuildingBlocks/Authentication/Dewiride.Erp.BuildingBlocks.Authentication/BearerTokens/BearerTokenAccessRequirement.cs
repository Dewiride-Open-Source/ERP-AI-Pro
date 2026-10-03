using Microsoft.AspNetCore.Authorization;

namespace Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;

internal sealed class BearerTokenAccessRequirement(BearerTokenAccess access) : IAuthorizationRequirement
{
    public BearerTokenAccess Access { get; } = access;
}
