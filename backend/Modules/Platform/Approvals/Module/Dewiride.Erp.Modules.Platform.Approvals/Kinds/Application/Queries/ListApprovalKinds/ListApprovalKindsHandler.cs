using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Approvals.Administration;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Approvals.Kinds.Application.Queries.ListApprovalKinds;

internal sealed class ListApprovalKindsHandler(IApprovalAdministration administration) : IQueryHandler<ListApprovalKindsQuery, IReadOnlyList<ApprovalKindDetails>>
{
    public async Task<Result<IReadOnlyList<ApprovalKindDetails>>> HandleAsync(ListApprovalKindsQuery query, CancellationToken cancellationToken) =>
        Result.Success(await administration.ListAsync(cancellationToken).ConfigureAwait(false));
}
