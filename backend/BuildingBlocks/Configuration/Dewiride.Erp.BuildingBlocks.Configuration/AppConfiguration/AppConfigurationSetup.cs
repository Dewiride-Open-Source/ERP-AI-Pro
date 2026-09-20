using Azure.Core;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Dewiride.Erp.BuildingBlocks.Configuration.AppConfiguration;

internal static class AppConfigurationSetup
{
    public const string SentinelKey = "Erp:Sentinel";

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
