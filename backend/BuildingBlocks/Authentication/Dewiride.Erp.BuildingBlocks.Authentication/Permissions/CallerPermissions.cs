using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Modules;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Permissions;

internal sealed class CallerPermissions(IHttpContextAccessor accessor, PermissionCatalog catalog) : ICallerPermissions
{
    private IReadOnlySet<string>? _held;

    public bool Holds(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        var request = accessor.HttpContext ?? throw new InvalidOperationException("The caller's permissions are read only during a request.");
        _held ??= catalog.PermissionsOf(CallerRoles.Of(request));

        return _held.Contains(permission);
    }
}
