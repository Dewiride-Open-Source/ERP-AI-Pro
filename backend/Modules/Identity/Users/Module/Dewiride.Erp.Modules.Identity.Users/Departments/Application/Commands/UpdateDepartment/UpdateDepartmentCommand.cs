using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Application.Commands.UpdateDepartment;

internal sealed record UpdateDepartmentCommand(Guid Id, string Name, Guid? ParentId, string Version) : ICommand<Guid>;
