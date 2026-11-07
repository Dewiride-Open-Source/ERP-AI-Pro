namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;

// A flag is written only when true. References name the sets whose row key a code must be on every day of the row; they are
// checked when the files are read and kept nowhere else.
internal sealed record SeedColumn(
    string Name,
    string Title,
    string Kind,
    bool? Key = null,
    bool? RowTitle = null,
    bool? Optional = null,
    decimal? Minimum = null,
    decimal? Maximum = null,
    IReadOnlyList<SeedChoice>? Choices = null,
    IReadOnlyList<string>? References = null);
