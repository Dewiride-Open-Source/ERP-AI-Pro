using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.ApplyAccountStates;

internal sealed record ApplyAccountStatesCommand(IReadOnlyDictionary<Guid, DirectoryAccountState> States) : ICommand<AccountCheckDetails>;
