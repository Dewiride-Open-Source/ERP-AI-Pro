namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;

internal sealed record SeedVersion(DateOnly From, IReadOnlyList<string> Instruments);
