namespace Dewiride.Erp.BuildingBlocks.Persistence.Migrations;

public sealed record MigrationStatus(string Schema, IReadOnlyList<string> Pending, IReadOnlyList<string> Unknown)
{
    public static MigrationStatus Of(string schema, IEnumerable<string> defined, IEnumerable<string> applied)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentNullException.ThrowIfNull(defined);
        ArgumentNullException.ThrowIfNull(applied);

        // EF Core's migrator matches the rows of the history table to the defined migrations ignoring case, so this does too.
        var definedIds = defined.ToList();
        var appliedIds = applied.ToList();
        var definedSet = definedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var appliedSet = appliedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new MigrationStatus(
            schema,
            [.. definedIds.Where(id => !appliedSet.Contains(id))],
            [.. appliedIds.Where(id => !definedSet.Contains(id))]);
    }
}
