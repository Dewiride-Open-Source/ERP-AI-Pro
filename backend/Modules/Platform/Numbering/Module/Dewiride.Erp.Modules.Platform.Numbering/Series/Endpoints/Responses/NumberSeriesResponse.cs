namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Endpoints.Responses;

/// <summary>The numbering of a kind of document.</summary>
/// <param name="Id">Identifier of the numbering.</param>
/// <param name="Format">How the numbers of the next financial year to open look.</param>
/// <param name="Version">Version of the numbering, sent back with a change.</param>
/// <param name="Years">The financial years opened so far, the latest first.</param>
internal sealed record NumberSeriesResponse(Guid Id, NumberFormatResponse Format, string Version, IReadOnlyList<NumberSeriesYearResponse> Years);
