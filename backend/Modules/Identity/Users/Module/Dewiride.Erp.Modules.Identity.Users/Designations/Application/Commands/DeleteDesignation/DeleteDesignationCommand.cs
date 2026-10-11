using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Application.Commands.DeleteDesignation;

internal sealed record DeleteDesignationCommand(Guid Id) : ICommand<Guid>;
