using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Domain;

internal sealed class ApiStartup : AggregateRoot<ApiStartupId>
{
    public const int ApplicationNameMaxLength = 100;

    public const int VersionMaxLength = 100;

    public const int FrameworkMaxLength = 50;

    public const int EnvironmentNameMaxLength = 50;

    public const int ConfigurationLabelMaxLength = 50;

    public const int MachineNameMaxLength = 100;

    private ApiStartup(ApiStartupId id)
        : base(id)
    {
    }

    public string ApplicationName { get; private set; } = string.Empty;

    public BuildInfo Build { get; private set; }

    public string EnvironmentName { get; private set; } = string.Empty;

    public string? ConfigurationLabel { get; private set; }

    public string MachineName { get; private set; } = string.Empty;

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public static Result<ApiStartup> Record(
        string applicationName,
        BuildInfo build,
        string environmentName,
        string? configurationLabel,
        string machineName,
        DateTimeOffset startedAt,
        DateTimeOffset recordedAt)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            return ApiStartupErrors.ApplicationNameRequired;
        }

        if (string.IsNullOrWhiteSpace(build.Version))
        {
            return ApiStartupErrors.VersionRequired;
        }

        if (string.IsNullOrWhiteSpace(build.Framework))
        {
            return ApiStartupErrors.FrameworkRequired;
        }

        if (string.IsNullOrWhiteSpace(environmentName))
        {
            return ApiStartupErrors.EnvironmentNameRequired;
        }

        if (string.IsNullOrWhiteSpace(machineName))
        {
            return ApiStartupErrors.MachineNameRequired;
        }

        if (recordedAt < startedAt)
        {
            return ApiStartupErrors.RecordedBeforeStart;
        }

        return new ApiStartup(ApiStartupId.Create())
        {
            ApplicationName = Truncate(applicationName, ApplicationNameMaxLength),
            Build = new BuildInfo(Truncate(build.Version, VersionMaxLength), Truncate(build.Framework, FrameworkMaxLength)),
            EnvironmentName = Truncate(environmentName, EnvironmentNameMaxLength),
            ConfigurationLabel = configurationLabel is null ? null : Truncate(configurationLabel, ConfigurationLabelMaxLength),
            MachineName = Truncate(machineName, MachineNameMaxLength),
            StartedAt = startedAt,
            RecordedAt = recordedAt,
        };
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
