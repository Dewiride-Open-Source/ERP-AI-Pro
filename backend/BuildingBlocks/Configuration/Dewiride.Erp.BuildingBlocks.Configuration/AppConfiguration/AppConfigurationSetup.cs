using Azure.Core;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Dewiride.Erp.BuildingBlocks.Configuration.AppConfiguration;

internal static class AppConfigurationSetup
{
    public const string SentinelKey = "Erp:Sentinel";

    public const string FeatureFlagSchemaVariable = "AZURE_APP_CONFIGURATION_FM_SCHEMA_COMPATIBILITY_DISABLED";

    // Microsoft.FeatureManagement consults the Microsoft schema before the .NET schema regardless of provider order,
    // so a store flag mapped to the .NET schema could never outrank an environment or appsettings override (ADR-0012).
    // The provider offers this switch only as a process environment variable, read when its options are constructed.
    public static void RequireMicrosoftFeatureFlagSchema() => Environment.SetEnvironmentVariable(FeatureFlagSchemaVariable, "true");

    public static void Configure(AzureAppConfigurationOptions options, Uri endpoint, string label, TokenCredential credential, AppConfigurationRefreshOptions refresh) =>
        options
            .Connect(endpoint, credential)
            .Select(KeyFilter.Any, LabelFilter.Null)
            .Select(KeyFilter.Any, label)
            .ConfigureRefresh(refreshOptions => refreshOptions
                .Register(SentinelKey, label, refreshAll: true)
                .SetRefreshInterval(refresh.RefreshInterval))
            .ConfigureKeyVault(keyVault => keyVault
                .SetCredential(credential)
                .SetSecretRefreshInterval(refresh.SecretRefreshInterval))
            .UseFeatureFlags(featureFlags => featureFlags
                .Select(KeyFilter.Any, LabelFilter.Null)
                .Select(KeyFilter.Any, label)
                .SetRefreshInterval(refresh.RefreshInterval))
            .ConfigureStartupOptions(startup => startup.Timeout = refresh.StartupTimeout);
}
