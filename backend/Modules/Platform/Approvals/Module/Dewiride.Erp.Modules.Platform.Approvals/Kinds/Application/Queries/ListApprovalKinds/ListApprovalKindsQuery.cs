using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Approvals.Administration;

namespace Dewiride.Erp.Modules.Platform.Approvals.Kinds.Application.Queries.ListApprovalKinds;

internal sealed record ListApprovalKindsQuery : IQuery<IReadOnlyList<ApprovalKindDetails>>;
