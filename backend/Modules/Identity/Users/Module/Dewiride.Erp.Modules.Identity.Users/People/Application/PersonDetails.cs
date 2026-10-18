using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Shared.Positions;
using Dewiride.Erp.Modules.Identity.Users.Shared.Versioning;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application;

internal sealed record PersonDetails(
    Guid Id,
    Guid? EntraObjectId,
    string DisplayName,
    string WorkEmail,
    string? EmployeeCode,
    string? PhoneNumber,
    DateOnly? DateOfJoining,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? DesignationId,
    string? DesignationName,
    Guid? ManagerId,
    string? ManagerDisplayName,
    bool? ManagerActive,
    PersonStatus Status,
    PersonDeactivationReason? DeactivatedBecause,
    IReadOnlyList<PersonRole> Roles,
    DateTimeOffset? LastSignedInAt,
    DateTimeOffset CreatedAt,
    string Version)
{
    public static PersonDetails From(PersonRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var user = row.User;

        return new(
            user.Id.Value,
            user.EntraObjectId,
            user.DisplayName,
            user.WorkEmail,
            user.EmployeeCode,
            user.PhoneNumber,
            user.DateOfJoining,
            row.DepartmentId?.Value,
            row.DepartmentName,
            row.DesignationId?.Value,
            row.DesignationName,
            row.ManagerId?.Value,
            row.ManagerDisplayName,
            row.ManagerActive,
            user.IsActive ? PersonStatus.Active : PersonStatus.Deactivated,
            user.DeactivatedBecause is { } reason ? DeactivationReasons.ToPersonDeactivationReason(reason) : null,
            [.. user.Roles.Where(User.IsAssignable).Select(PersonRoles.ToPersonRole)],
            user.LastSignedInAt,
            user.CreatedAt,
            RecordVersion.Encode(user.RowVersion));
    }
}
