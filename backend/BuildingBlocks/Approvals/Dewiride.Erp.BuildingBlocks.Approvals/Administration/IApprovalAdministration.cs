using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Administration;

// Administrators switch approval off for a kind, so its records go through without a request and nobody but the person who made
// them looks at them, or change the working days its steps have. A change applies to the requests submitted after it; a waiting
// request keeps its route and its due times.
public interface IApprovalAdministration
{
    Task<IReadOnlyList<ApprovalKindDetails>> ListAsync(CancellationToken cancellationToken);

    Task<Result<ApprovalKindDetails>> SetUpAsync(string kind, bool required, int workingDaysToDecide, CancellationToken cancellationToken);

    Task<Result<ApprovalKindDetails>> ChangeAsync(ApprovalKindSettingId id, byte[] version, bool required, int workingDaysToDecide, CancellationToken cancellationToken);
}
