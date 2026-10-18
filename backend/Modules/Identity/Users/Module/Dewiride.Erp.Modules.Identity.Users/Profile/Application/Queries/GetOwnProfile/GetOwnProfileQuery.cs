using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.Modules.Identity.Users.Profile.Application.Queries.GetOwnProfile;

internal sealed record GetOwnProfileQuery(IReadOnlySet<ErpRole> HeldRoles) : IQuery<OwnProfile>;
