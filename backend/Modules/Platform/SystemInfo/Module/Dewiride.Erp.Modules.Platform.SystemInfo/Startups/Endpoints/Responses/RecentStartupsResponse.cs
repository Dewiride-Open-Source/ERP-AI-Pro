namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Endpoints.Responses;

/// <summary>The most recent starts of the API, newest first.</summary>
/// <param name="Startups">Recorded starts ordered by start time descending.</param>
internal sealed record RecentStartupsResponse(IReadOnlyList<StartupResponse> Startups);
