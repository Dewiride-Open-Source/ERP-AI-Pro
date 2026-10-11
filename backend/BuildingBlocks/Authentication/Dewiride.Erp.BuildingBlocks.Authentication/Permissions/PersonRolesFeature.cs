using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Permissions;

// Set on the request only once the session check or the bearer token check has accepted the person, so the roles of a
// record never outlive a refusal; the cookie itself never carries them, so a renewal never writes them into it.
internal sealed record PersonRolesFeature(Guid ObjectId, IReadOnlySet<ErpRole> AssignedRoles);
