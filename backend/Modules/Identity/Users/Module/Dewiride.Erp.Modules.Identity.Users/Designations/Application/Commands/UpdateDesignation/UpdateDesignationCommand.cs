using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Application.Commands.UpdateDesignation;

internal sealed record UpdateDesignationCommand(Guid Id, string Name, string Version) : ICommand<Guid>;
