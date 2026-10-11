using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.InvitePerson;

internal sealed record InvitePersonCommand(
    DirectoryPerson Person,
    string? EmployeeCode,
    string? PhoneNumber,
    string? Designation,
    DateOnly? DateOfJoining,
    IReadOnlyCollection<PersonRole> Roles) : ICommand<Guid>;
