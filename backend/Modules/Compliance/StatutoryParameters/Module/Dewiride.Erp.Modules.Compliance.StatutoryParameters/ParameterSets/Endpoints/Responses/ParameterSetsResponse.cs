namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;

/// <summary>The statutory parameter sets the ERP holds.</summary>
/// <param name="Sets">The sets, by title.</param>
internal sealed record ParameterSetsResponse(IReadOnlyList<ParameterSetSummaryResponse> Sets);
