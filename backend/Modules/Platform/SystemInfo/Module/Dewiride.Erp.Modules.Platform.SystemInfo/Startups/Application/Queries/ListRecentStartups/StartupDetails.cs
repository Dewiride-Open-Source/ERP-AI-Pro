namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Application.Queries.ListRecentStartups;

internal sealed record StartupDetails(
    Guid Id,
    string ApplicationName,
    string Version,
    string Framework,
    string EnvironmentName,
    string? ConfigurationLabel,
    string MachineName,
    DateTimeOffset StartedAt,
    DateTimeOffset RecordedAt);
