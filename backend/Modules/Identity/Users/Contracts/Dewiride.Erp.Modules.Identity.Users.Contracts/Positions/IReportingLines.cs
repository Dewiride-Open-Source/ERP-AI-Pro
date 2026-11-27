namespace Dewiride.Erp.Modules.Identity.Users.Contracts.Positions;

// A person's managers on a date, nearest first: their manager on that date, that manager's manager on that date, and so on up to the
// top, each listed once and the person never. The answer is empty when no record that is not deleted is linked to the object id, or
// when the person holds no position on that date or holds one that names no manager; a manager whose record is deactivated or
// deleted is listed inactive, and one who has never signed in has no object id.
public interface IReportingLines
{
    Task<IReadOnlyList<ReportingManagerEntry>> ManagersOfAsync(Guid objectId, DateOnly on, CancellationToken cancellationToken);
}
