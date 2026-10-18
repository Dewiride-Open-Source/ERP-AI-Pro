using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.InvitePerson;

internal sealed record InvitePersonCommand(
    DirectoryPerson Person,
    string? EmployeeCode,
    string? PhoneNumber,
    DateOnly? DateOfJoining,
    IReadOnlyCollection<PersonRole> Roles) : ICommand<Guid>;
