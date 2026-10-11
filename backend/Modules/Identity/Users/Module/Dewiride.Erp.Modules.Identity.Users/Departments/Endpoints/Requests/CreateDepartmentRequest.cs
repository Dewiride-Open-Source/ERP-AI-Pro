using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.Modules.Identity.Users.Departments.Application;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Endpoints.Requests;

public sealed record CreateDepartmentRequest(
    [property: Description("Name of the department, unique among the departments under the same parent.")]
    [property: Required]
    [property: StringLength(DepartmentLimits.NameMaxLength)]
    string? Name,
    [property: Description("Department it sits under; absent places it at the top of the tree.")]
    Guid? ParentId);
