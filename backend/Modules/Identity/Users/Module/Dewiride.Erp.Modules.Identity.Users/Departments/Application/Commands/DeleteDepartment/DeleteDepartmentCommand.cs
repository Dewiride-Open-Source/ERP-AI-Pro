using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Application.Commands.DeleteDepartment;

internal sealed record DeleteDepartmentCommand(Guid Id) : ICommand<Guid>;
