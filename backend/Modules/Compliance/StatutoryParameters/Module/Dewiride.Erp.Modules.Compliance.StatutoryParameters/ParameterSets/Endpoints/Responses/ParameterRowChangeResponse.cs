namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;

/// <summary>An entry a version changed.</summary>
/// <param name="Before">The entry as it was the day before the version.</param>
/// <param name="After">The entry as it is from the version's date.</param>
internal sealed record ParameterRowChangeResponse(ParameterRowResponse Before, ParameterRowResponse After);
