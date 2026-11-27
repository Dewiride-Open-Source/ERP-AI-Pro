using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.Modules.Identity.Users.Contracts.Positions;

namespace Dewiride.Erp.Modules.Platform.Approvals.Integration;

// A request names people by their Microsoft Entra object id, so a manager step is routed by the object ids of the managers the
// users module answers, nearest first; the ids of their records stay that module's.
internal sealed class UsersReportingManagers(IReportingLines reportingLines) : IReportingManagers
{
    public async Task<IReadOnlyList<ReportingManager>> ManagersOfAsync(Guid objectId, DateOnly on, CancellationToken cancellationToken)
    {
        var managers = await reportingLines.ManagersOfAsync(objectId, on, cancellationToken).ConfigureAwait(false);

        return [.. managers.Select(manager => new ReportingManager(manager.ObjectId, manager.IsActive))];
    }
}
