using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Application.Queries.ListDepartments;

internal sealed record ListDepartmentsQuery(ListRequest Request) : IQuery<PagedResult<DepartmentDetails>>;
