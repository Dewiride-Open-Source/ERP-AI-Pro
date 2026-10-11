using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.RegisterPerson;

internal sealed record RegisterPersonCommand(
    string DisplayName,
    string WorkEmail,
    string? EmployeeCode,
    string? PhoneNumber,
    string? Designation,
    DateOnly? DateOfJoining,
    IReadOnlyCollection<PersonRole> Roles) : ICommand<Guid>;
