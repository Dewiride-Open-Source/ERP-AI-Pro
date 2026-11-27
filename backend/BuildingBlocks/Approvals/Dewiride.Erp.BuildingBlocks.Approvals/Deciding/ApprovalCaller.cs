using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Deciding;

// The person reading or deciding, as their request carries them: their Microsoft Entra object id, the built-in roles they hold,
// whether they may see every request, and whether they may decide a request that waits for somebody else, in that person's place.
public sealed record ApprovalCaller(Guid ObjectId, IReadOnlySet<ErpRole> Roles, bool SeesAll, bool DecidesAny);
