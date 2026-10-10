using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.AdmitPerson;

internal sealed record AdmitPersonCommand(Guid EntraObjectId, string DisplayName, string WorkEmail) : ICommand<bool>;
