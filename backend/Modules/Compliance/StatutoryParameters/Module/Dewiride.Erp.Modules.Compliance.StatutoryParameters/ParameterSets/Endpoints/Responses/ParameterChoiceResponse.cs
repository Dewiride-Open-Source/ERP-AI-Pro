namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;

/// <summary>One value a choice column holds.</summary>
/// <param name="Value">The value as a cell holds it.</param>
/// <param name="Title">Title of the value.</param>
internal sealed record ParameterChoiceResponse(string Value, string Title);
