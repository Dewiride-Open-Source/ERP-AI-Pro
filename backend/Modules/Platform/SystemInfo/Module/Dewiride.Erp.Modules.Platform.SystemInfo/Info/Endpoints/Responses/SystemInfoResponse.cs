namespace Dewiride.Erp.Modules.Platform.SystemInfo.Info.Endpoints.Responses;

/// <summary>Identity and uptime of the running API.</summary>
/// <param name="ApplicationName">Configured display name of the application.</param>
/// <param name="Version">Informational version of the API host assembly.</param>
/// <param name="StartedAt">UTC time the process started.</param>
/// <param name="UptimeSeconds">Seconds elapsed since the process started.</param>
internal sealed record SystemInfoResponse(string ApplicationName, string Version, DateTimeOffset StartedAt, double UptimeSeconds);
