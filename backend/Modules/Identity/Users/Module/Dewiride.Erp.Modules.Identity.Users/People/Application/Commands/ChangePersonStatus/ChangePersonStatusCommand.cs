using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.ChangePersonStatus;

internal sealed record ChangePersonStatusCommand(Guid Id, PersonStatus Status, string Version) : ICommand<Guid>;
