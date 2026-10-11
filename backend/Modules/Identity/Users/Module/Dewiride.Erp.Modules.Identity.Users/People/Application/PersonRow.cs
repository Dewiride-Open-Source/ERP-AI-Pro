using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application;

// Built with an object initializer: EF Core filters and sorts a projection only through the members it assigned, never through
// constructor parameters.
internal sealed record PersonRow
{
    public required User User { get; init; }

    public DepartmentId? DepartmentId { get; init; }

    public string? DepartmentName { get; init; }

    public DesignationId? DesignationId { get; init; }

    public string? DesignationName { get; init; }

    public UserId? ManagerId { get; init; }

    public string? ManagerDisplayName { get; init; }
}
