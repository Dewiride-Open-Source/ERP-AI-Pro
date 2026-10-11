namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application;

internal sealed record PositionDetails(
    Guid Id,
    DateOnly EffectiveFrom,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? DesignationId,
    string? DesignationName,
    Guid? ManagerId,
    string? ManagerDisplayName)
{
    public static PositionDetails From(PositionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var position = row.Position;

        return new(
            position.Id.Value,
            position.EffectiveFrom,
            position.DepartmentId?.Value,
            row.DepartmentName,
            position.DesignationId?.Value,
            row.DesignationName,
            position.ManagerId?.Value,
            row.ManagerDisplayName);
    }
}
