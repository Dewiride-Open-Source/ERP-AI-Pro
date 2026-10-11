namespace Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Responses;

/// <summary>A position a person held, holds or will hold, from its date until the date their next position starts.</summary>
/// <param name="Id">Identifier of the position.</param>
/// <param name="EffectiveFrom">Date the position starts.</param>
/// <param name="DepartmentId">Department the person is in.</param>
/// <param name="DepartmentName">Name of that department, kept after it is deleted.</param>
/// <param name="DesignationId">Designation the person holds.</param>
/// <param name="DesignationName">Name of that designation, kept after it is deleted.</param>
/// <param name="ManagerId">Record of the person they report to.</param>
/// <param name="ManagerDisplayName">Display name of that manager, kept after their record is deleted.</param>
internal sealed record PositionResponse(
    Guid Id,
    DateOnly EffectiveFrom,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? DesignationId,
    string? DesignationName,
    Guid? ManagerId,
    string? ManagerDisplayName);
