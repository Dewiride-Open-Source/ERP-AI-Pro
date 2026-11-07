using System.Text.Json;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;

// The cells stay JSON until the columns are known, because a cell is a string or a number by its column's kind.
internal sealed record SeedRow(DateOnly From, string Source, JsonElement Cells, DateOnly? Until = null);
