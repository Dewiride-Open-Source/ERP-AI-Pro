namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;

public sealed record ParameterVersionEntry(DateOnly From, DateOnly? Until, IReadOnlyList<string> Instruments, int AddedCount, int ChangedCount, int RemovedCount);
