using Dewiride.Erp.Modules.Identity.Users.Positions.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application;

// Built with an object initializer: EF Core filters and sorts a projection only through the members it assigned, never through
// constructor parameters.
internal sealed record PositionRow
{
    public required Position Position { get; init; }

    public string? DepartmentName { get; init; }

    public string? DesignationName { get; init; }

    public string? ManagerDisplayName { get; init; }
}
