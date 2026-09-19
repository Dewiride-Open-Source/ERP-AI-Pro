namespace Dewiride.Erp.Modules.Platform.SystemInfo.Info.Application.Queries.GetSystemInfo;

internal sealed record SystemInfoDetails(string ApplicationName, string Version, DateTimeOffset StartedAt, TimeSpan Uptime);
