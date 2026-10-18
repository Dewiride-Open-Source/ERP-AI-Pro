using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.Modules.Identity.Users.Profile.Application;

internal sealed record OwnProfile(
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
    ChosenTheme Theme);
