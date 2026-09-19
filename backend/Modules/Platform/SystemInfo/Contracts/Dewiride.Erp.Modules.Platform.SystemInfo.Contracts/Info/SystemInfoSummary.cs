namespace Dewiride.Erp.Modules.Platform.SystemInfo.Contracts.Info;

public sealed record SystemInfoSummary(string ApplicationName, string Version, DateTimeOffset StartedAt);
