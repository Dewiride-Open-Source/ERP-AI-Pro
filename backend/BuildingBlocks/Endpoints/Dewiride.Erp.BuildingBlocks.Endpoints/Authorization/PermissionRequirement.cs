using Microsoft.AspNetCore.Authorization;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
