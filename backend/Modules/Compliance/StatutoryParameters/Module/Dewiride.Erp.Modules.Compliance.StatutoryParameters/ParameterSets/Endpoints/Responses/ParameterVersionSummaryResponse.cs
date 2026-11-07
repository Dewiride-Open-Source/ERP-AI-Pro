namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;

/// <summary>One version of a set: a legal change, with the instruments that brought it and how many entries it changed.</summary>
/// <param name="From">Date the version applies from.</param>
/// <param name="Until">Last date the version applies, the day before the next version; null for the latest version.</param>
/// <param name="Instruments">The notifications and rules that brought the version, as the ERP cites them.</param>
/// <param name="AddedCount">Number of entries the version added.</param>
/// <param name="ChangedCount">Number of entries the version changed.</param>
/// <param name="RemovedCount">Number of entries the version removed.</param>
internal sealed record ParameterVersionSummaryResponse(DateOnly From, DateOnly? Until, IReadOnlyList<string> Instruments, int AddedCount, int ChangedCount, int RemovedCount);
