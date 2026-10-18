using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;

namespace Dewiride.Erp.Modules.Identity.Users.Profile.Endpoints.Responses;

/// <summary>The signed-in person's own record.</summary>
/// <param name="Id">Identifier of the person's record.</param>
/// <param name="DisplayName">Name the ERP shows for the person, which each sign-in brings from Entra.</param>
/// <param name="WorkEmail">Work email of the person, in lower case, which each sign-in brings from Entra.</param>
/// <param name="EmployeeCode">Employee code an administrator recorded for the person.</param>
/// <param name="PhoneNumber">Phone number in international form, + and its country code first.</param>
/// <param name="DateOfJoining">Date the person joined the company.</param>
/// <param name="DepartmentName">Name of the department of the position the person holds today.</param>
/// <param name="DesignationName">Name of the designation of the position the person holds today.</param>
/// <param name="ManagerDisplayName">Display name of the person they report to today.</param>
/// <param name="Roles">Built-in roles the person holds, in the order the ERP lists them: Administrator and Employee through their app roles in Microsoft Entra, the others assigned in the ERP.</param>
/// <param name="Theme">Colour theme the person chose for the web app.</param>
internal sealed record ProfileResponse(
    Guid Id,
    string DisplayName,
    string WorkEmail,
    string? EmployeeCode,
    string? PhoneNumber,
    DateOnly? DateOfJoining,
    string? DepartmentName,
    string? DesignationName,
    string? ManagerDisplayName,
    IReadOnlyList<ErpRole> Roles,
    ThemePreference Theme);
