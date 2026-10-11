using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.AssignPersonRoles;

internal sealed record AssignPersonRolesCommand(Guid Id, IReadOnlyCollection<PersonRole> Roles, string Version) : ICommand<Guid>;
