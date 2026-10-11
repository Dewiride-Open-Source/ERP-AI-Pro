using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.Modules.Identity.Users.Departments.Application;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Endpoints.Requests;

public sealed record UpdateDepartmentRequest(
    [property: Description("Name of the department, unique among the departments under the same parent.")]
    [property: Required]
    [property: StringLength(DepartmentLimits.NameMaxLength)]
    string? Name,
    [property: Description("Department it sits under from now on, never itself or one within it; absent moves it to the top of the tree.")]
    Guid? ParentId,
    [property: Description("Version of the department the change was made from, as the API last returned it.")]
    [property: Required]
    [property: StringLength(DepartmentLimits.VersionMaxLength)]
    string? Version);
