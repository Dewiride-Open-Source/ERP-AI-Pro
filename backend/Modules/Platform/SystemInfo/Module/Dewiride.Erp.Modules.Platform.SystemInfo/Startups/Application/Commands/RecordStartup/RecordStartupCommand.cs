using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Domain;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Application.Commands.RecordStartup;

internal sealed record RecordStartupCommand(
    string ApplicationName,
    BuildInfo Build,
    string EnvironmentName,
    string? ConfigurationLabel,
    string MachineName,
    DateTimeOffset StartedAt) : ICommand<ApiStartupId>;
