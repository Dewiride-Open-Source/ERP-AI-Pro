namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Endpoints.Responses;

/// <summary>One recorded start of the API.</summary>
/// <param name="Id">Identifier of the recorded start.</param>
/// <param name="ApplicationName">Configured display name of the application at that start.</param>
/// <param name="Version">Informational version of the API host assembly at that start.</param>
/// <param name="Framework">.NET runtime the process ran on.</param>
/// <param name="EnvironmentName">ASP.NET Core environment name of the process.</param>
/// <param name="ConfigurationLabel">App Configuration label the process loaded, or null when it ran without the store.</param>
/// <param name="StartedAt">UTC time the process started.</param>
/// <param name="RecordedAt">UTC time the start was written to the database.</param>
internal sealed record StartupResponse(
    Guid Id,
    string ApplicationName,
    string Version,
    string Framework,
    string EnvironmentName,
    string? ConfigurationLabel,
    DateTimeOffset StartedAt,
    DateTimeOffset RecordedAt);
