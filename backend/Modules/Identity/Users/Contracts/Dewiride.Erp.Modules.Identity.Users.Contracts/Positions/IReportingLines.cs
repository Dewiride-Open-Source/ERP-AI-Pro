namespace Dewiride.Erp.Modules.Identity.Users.Contracts.Positions;

// A person's managers on a date, nearest first: their manager on that date, that manager's manager on that date, and so on up to the
// top. The answer is empty when no record is linked to the object id or the person's position names no manager on that date; a
// manager whose record is deactivated or deleted is listed inactive, and one who has never signed in has no object id.
public interface IReportingLines
{
    Task<IReadOnlyList<ReportingManagerEntry>> ManagersOfAsync(Guid objectId, DateOnly on, CancellationToken cancellationToken);
}
