namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;

// The shape of a seed file, read strictly: its basis, as-of rule and column kinds are camelCase names compared exactly.
internal sealed record StatutorySeedFile(
    string Key,
    string Title,
    string Description,
    string Basis,
    string AsOf,
    IReadOnlyList<SeedColumn> Columns,
    IReadOnlyList<SeedVersion> Versions,
    IReadOnlyList<SeedRow> Rows);
