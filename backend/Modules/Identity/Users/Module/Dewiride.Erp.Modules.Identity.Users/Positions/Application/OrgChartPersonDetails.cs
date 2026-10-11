using Dewiride.Erp.Modules.Identity.Users.Shared.Positions;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application;

internal sealed record OrgChartPersonDetails(
    Guid Id,
    string DisplayName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? DesignationId,
    string? DesignationName,
    Guid? ManagerId,
    string? ManagerDisplayName)
{
    public static OrgChartPersonDetails From(PersonRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new(
            row.User.Id.Value,
            row.User.DisplayName,
            row.DepartmentId?.Value,
            row.DepartmentName,
            row.DesignationId?.Value,
            row.DesignationName,
            row.ManagerId?.Value,
            row.ManagerDisplayName);
    }
}
