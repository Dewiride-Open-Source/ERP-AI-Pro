using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Domain;

internal static class StartupErrors
{
    public static readonly Error ApplicationNameRequired = Error.Validation("startup.application-name-required", "The application name of a start must not be empty.");

    public static readonly Error VersionRequired = Error.Validation("startup.version-required", "The build version of a start must not be empty.");

    public static readonly Error FrameworkRequired = Error.Validation("startup.framework-required", "The framework description of a start must not be empty.");

    public static readonly Error EnvironmentNameRequired = Error.Validation("startup.environment-name-required", "The environment name of a start must not be empty.");

    public static readonly Error MachineNameRequired = Error.Validation("startup.machine-name-required", "The machine name of a start must not be empty.");

    public static readonly Error RecordedBeforeStart = Error.Validation("startup.recorded-before-start", "A start cannot be recorded before the process started.");
}
