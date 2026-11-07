namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;

public sealed record StatutoryParameterVersion(DateOnly From, DateOnly? Until, IReadOnlyList<string> Instruments);
