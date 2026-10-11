namespace Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Responses;

/// <summary>A person on the organisation chart of a date, with the position they hold on it.</summary>
/// <param name="Id">Identifier of the person's record.</param>
/// <param name="DisplayName">Name the ERP shows for the person.</param>
/// <param name="DepartmentId">Department the person is in.</param>
/// <param name="DepartmentName">Name of that department.</param>
/// <param name="DesignationId">Designation the person holds.</param>
/// <param name="DesignationName">Name of that designation.</param>
/// <param name="ManagerId">Record of the person they report to, who is outside the chart when that record is deactivated.</param>
/// <param name="ManagerDisplayName">Display name of that manager.</param>
internal sealed record OrgChartPersonResponse(
    Guid Id,
    string DisplayName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? DesignationId,
    string? DesignationName,
    Guid? ManagerId,
    string? ManagerDisplayName);
