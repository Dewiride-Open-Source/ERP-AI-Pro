using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.Modules.Identity.Users.Permissions.Endpoints.Responses;

/// <summary>A permission and the built-in roles that hold it.</summary>
/// <param name="Name">Name of the permission, four segments: domain, module, feature and action.</param>
/// <param name="Description">What a holder of the permission may do, in one sentence.</param>
/// <param name="HeldBy">Built-in roles that hold the permission, in the order of the catalogue's roles; Administrator holds every permission, so it is always among them.</param>
internal sealed record PermissionResponse(string Name, string Description, IReadOnlyList<ErpRole> HeldBy);
