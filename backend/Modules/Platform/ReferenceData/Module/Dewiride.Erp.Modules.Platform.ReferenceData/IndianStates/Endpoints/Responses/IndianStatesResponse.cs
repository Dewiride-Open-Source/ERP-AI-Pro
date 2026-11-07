namespace Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Endpoints.Responses;

/// <summary>India's States and Union Territories with their GST State codes.</summary>
/// <param name="States">The State codes, by code and then by the date each starts.</param>
internal sealed record IndianStatesResponse(IReadOnlyList<IndianStateResponse> States);
