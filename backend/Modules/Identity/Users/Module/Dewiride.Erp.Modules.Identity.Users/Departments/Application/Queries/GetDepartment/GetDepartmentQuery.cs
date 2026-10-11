using Dewiride.Erp.BuildingBlocks.Application.Queries;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Application.Queries.GetDepartment;

internal sealed record GetDepartmentQuery(Guid Id) : IQuery<DepartmentDetails>;
