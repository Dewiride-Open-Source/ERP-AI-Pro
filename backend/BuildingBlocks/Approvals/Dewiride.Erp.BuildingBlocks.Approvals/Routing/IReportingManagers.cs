namespace Dewiride.Erp.BuildingBlocks.Approvals.Routing;

// The people's reporting lines are a module's, so that module answers a person's managers on a date, nearest first: their manager
// on that date, that manager's manager on that date, and so on up to the top. The answer is empty for a person the ERP holds no
// record of or whose position names no manager on that date; a manager whose record is deactivated or deleted is listed inactive,
// and one who has never signed in has no object id.
public interface IReportingManagers
{
    Task<IReadOnlyList<ReportingManager>> ManagersOfAsync(Guid objectId, DateOnly on, CancellationToken cancellationToken);
}
