using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.InvitePerson;

internal sealed record InvitePersonCommand(
    DirectoryPerson Person,
    string? EmployeeCode,
    string? PhoneNumber,
    string? Designation,
    DateOnly? DateOfJoining) : ICommand<Guid>;
