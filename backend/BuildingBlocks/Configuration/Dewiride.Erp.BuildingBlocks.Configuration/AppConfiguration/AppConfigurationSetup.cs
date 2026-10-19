using Azure.Core;
using Azure.Data.AppConfiguration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Dewiride.Erp.BuildingBlocks.Configuration.AppConfiguration;

internal static class AppConfigurationSetup
{
    public const string SentinelKey = "Erp:Sentinel";

    public const string FeatureFlagSchemaVariable = "AZURE_APP_CONFIGURATION_FM_SCHEMA_COMPATIBILITY_DISABLED";

    public const string KeyVaultReferenceContentType = "application/vnd.microsoft.appconfig.keyvaultref+json";

    // Every other source defines flags in the Microsoft schema (ADR-0012); without this switch the store maps a plain flag
    // to the .NET schema, so the same flag would read differently depending on where it is defined.
    // The provider offers this switch only as a process environment variable, read when its options are constructed.
    public static void RequireMicrosoftFeatureFlagSchema() => Environment.SetEnvironmentVariable(FeatureFlagSchemaVariable, "true");

    public static void Configure(
        AzureAppConfigurationOptions options,
        Uri endpoint,
        string label,
        TokenCredential credential,
        AppConfigurationRefreshOptions refresh,
        IReadOnlySet<string>? keyVaultReferences)
    {
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

        if (keyVaultReferences is not null)
        {
            options.Map(setting => ValueTask.FromResult(WithoutUnnamedReference(setting, keyVaultReferences)));
        }
    }

    public static ConfigurationSetting WithoutUnnamedReference(ConfigurationSetting setting, IReadOnlySet<string> keyVaultReferences)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(keyVaultReferences);

        return IsKeyVaultReference(setting) && !keyVaultReferences.Contains(setting.Key)
            ? new ConfigurationSetting(setting.Key, string.Empty, setting.Label, setting.ETag)
            : setting;
    }

    private static bool IsKeyVaultReference(ConfigurationSetting setting) =>
        setting.ContentType is { } contentType && contentType.StartsWith(KeyVaultReferenceContentType, StringComparison.OrdinalIgnoreCase);
}
