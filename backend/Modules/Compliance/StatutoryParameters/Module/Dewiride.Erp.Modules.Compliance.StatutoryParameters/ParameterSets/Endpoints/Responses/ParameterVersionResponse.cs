namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;

/// <summary>One version of a set with its entries and what it changed from the version before it.</summary>
/// <param name="From">Date the version applies from.</param>
/// <param name="Until">Last date the version applies, the day before the next version; null for the latest version.</param>
/// <param name="Instruments">The notifications and rules that brought the version, as the ERP cites them.</param>
/// <param name="Rows">The entries in force throughout the version, by key.</param>
/// <param name="Added">The entries the version added, by key.</param>
/// <param name="Changed">The entries the version changed, each as it was the day before and as it is from the version's date, by key.</param>
/// <param name="Removed">The entries the version removed, as they were the day before, by key.</param>
internal sealed record ParameterVersionResponse(
    DateOnly From,
    DateOnly? Until,
    IReadOnlyList<string> Instruments,
    IReadOnlyList<ParameterRowResponse> Rows,
    IReadOnlyList<ParameterRowResponse> Added,
    IReadOnlyList<ParameterRowChangeResponse> Changed,
    IReadOnlyList<ParameterRowResponse> Removed);
