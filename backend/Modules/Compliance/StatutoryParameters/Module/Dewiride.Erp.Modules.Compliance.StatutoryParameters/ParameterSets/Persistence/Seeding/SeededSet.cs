using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;

// A seed file read and checked, its set with the versions and rows it declares, and the sets each code column references by
// column name.
internal sealed record SeededSet(
    ParameterSet Set,
    IReadOnlyList<ParameterVersion> Versions,
    IReadOnlyList<ParameterRow> Rows,
    IReadOnlyDictionary<string, IReadOnlyList<string>> References);
