using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.UpdatePerson;

internal sealed record UpdatePersonCommand(
    Guid Id,
    string? EmployeeCode,
    string? PhoneNumber,
    string? Designation,
    DateOnly? DateOfJoining,
    string Version) : ICommand<Guid>;
