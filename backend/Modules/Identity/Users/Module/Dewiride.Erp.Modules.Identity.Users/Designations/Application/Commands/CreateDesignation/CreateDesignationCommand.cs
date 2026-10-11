using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Application.Commands.CreateDesignation;

internal sealed record CreateDesignationCommand(string Name) : ICommand<Guid>;
