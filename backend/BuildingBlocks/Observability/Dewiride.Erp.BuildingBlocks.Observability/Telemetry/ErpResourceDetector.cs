using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;

namespace Dewiride.Erp.BuildingBlocks.Observability.Telemetry;

internal sealed class ErpResourceDetector(ApplicationInfo application, ErpConfigurationInfo configuration, IHostEnvironment environment) : IResourceDetector
{
    public const string ServiceVersionAttribute = "service.version";

    public const string DeploymentEnvironmentAttribute = "deployment.environment.name";

    public Resource Detect() => new(new Dictionary<string, object>
    {
        [ServiceVersionAttribute] = application.Version,
        [DeploymentEnvironmentAttribute] = configuration.Label ?? environment.EnvironmentName,
    });
}
