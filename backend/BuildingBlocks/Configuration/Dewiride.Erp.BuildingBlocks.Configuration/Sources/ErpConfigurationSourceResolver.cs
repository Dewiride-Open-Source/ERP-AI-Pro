using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.Configuration.Sources;

internal static class ErpConfigurationSourceResolver
{
    public const string EndpointVariable = "APPCONFIG_ENDPOINT";

    public const string SourceSetting = "ERP_CONFIGURATION_SOURCE";

    public const string InMemorySource = "InMemory";

    public static ErpConfigurationInfo Resolve(IConfiguration bootstrap, IHostEnvironment environment)
    {
        var endpointValue = bootstrap[EndpointVariable];
        if (!string.IsNullOrWhiteSpace(endpointValue))
        {
            if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint)
                || !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{EndpointVariable} must be an absolute https URI such as https://<store>.azconfig.io; the configured value was rejected.");
            }

            var label = bootstrap[ErpEnvironmentNames.VariableName];
            if (!ErpEnvironmentNames.IsKnown(label))
            {
                throw new InvalidOperationException(
                    $"{ErpEnvironmentNames.VariableName} must be '{ErpEnvironmentNames.LocalDev}' or '{ErpEnvironmentNames.Production}'; value '{label}' was rejected.");
            }

            return new ErpConfigurationInfo(ErpConfigurationSource.AppConfiguration, label, endpoint);
        }

        if (string.Equals(bootstrap[SourceSetting], InMemorySource, StringComparison.OrdinalIgnoreCase))
        {
            return new ErpConfigurationInfo(ErpConfigurationSource.InMemory, null, null);
        }

        if (environment.IsDevelopment())
        {
            return new ErpConfigurationInfo(ErpConfigurationSource.LocalDevelopment, null, null);
        }

        throw new InvalidOperationException(
            $"{EndpointVariable} is required when ASPNETCORE_ENVIRONMENT is '{environment.EnvironmentName}'. " +
            "Set it to the Azure App Configuration endpoint (https://<store>.azconfig.io).");
    }
}
