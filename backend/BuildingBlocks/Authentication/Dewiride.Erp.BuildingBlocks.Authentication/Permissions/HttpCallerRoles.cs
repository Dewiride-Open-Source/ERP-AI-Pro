using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Permissions;

internal sealed class HttpCallerRoles(IHttpContextAccessor accessor) : ICallerRoles
{
    public IReadOnlySet<ErpRole> Roles =>
        CallerRoles.Of(accessor.HttpContext ?? throw new InvalidOperationException("The caller's roles are read only during a request."));
}
