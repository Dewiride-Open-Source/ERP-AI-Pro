namespace Dewiride.Erp.Modules.Identity.Users.Departments.Endpoints.Responses;

/// <summary>A department of the company.</summary>
/// <param name="Id">Identifier of the department.</param>
/// <param name="Name">Name of the department, unique among the departments under the same parent.</param>
/// <param name="ParentId">Department it sits under; absent at the top of the tree.</param>
/// <param name="Version">Version of the department, which a change names to be refused when the department changed since.</param>
internal sealed record DepartmentResponse(Guid Id, string Name, Guid? ParentId, string Version);
