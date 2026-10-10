using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.DeletePerson;

internal sealed record DeletePersonCommand(Guid Id) : ICommand<Guid>;
