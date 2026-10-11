using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Application.Commands.CreateDepartment;

internal sealed record CreateDepartmentCommand(string Name, Guid? ParentId) : ICommand<Guid>;
