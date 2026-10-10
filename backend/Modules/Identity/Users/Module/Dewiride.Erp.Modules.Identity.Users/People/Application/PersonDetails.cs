using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application;

internal sealed record PersonDetails(
    Guid Id,
    Guid? EntraObjectId,
    string DisplayName,
    string WorkEmail,
    string? EmployeeCode,
    string? PhoneNumber,
    string? Designation,
    DateOnly? DateOfJoining,
    PersonStatus Status,
    DateTimeOffset? LastSignedInAt,
    DateTimeOffset CreatedAt,
    string Version)
{
    public static PersonDetails From(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new(
            user.Id.Value,
            user.EntraObjectId,
            user.DisplayName,
            user.WorkEmail,
            user.EmployeeCode,
            user.PhoneNumber,
            user.Designation,
            user.DateOfJoining,
            user.IsActive ? PersonStatus.Active : PersonStatus.Deactivated,
            user.LastSignedInAt,
            user.CreatedAt,
            PersonVersion.Encode(user.RowVersion));
    }
}
