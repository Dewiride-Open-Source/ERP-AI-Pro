using Azure.Core;
using Dewiride.Erp.BuildingBlocks.Configuration.AppConfiguration;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.AppConfiguration.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.AppConfiguration;

public sealed class AppConfigurationSetupTests
{
    private const string ApplicationNameKey = "Erp:Platform:Host:ApplicationName";
    private const string FeatureFlagKeyFilter = ".appconfig.featureflag/*";

    [Fact]
    public void Configure_LoadsUnlabelledThenLabelledKeysAndFeatureFlags()
    {
        var client = new FakeConfigurationClient();

        Load(client, ErpEnvironmentNames.LocalDev);

        Assert.Equal(
            [
                (KeyFilter.Any, LabelFilter.Null),
                (KeyFilter.Any, ErpEnvironmentNames.LocalDev),
                (FeatureFlagKeyFilter, LabelFilter.Null),
                (FeatureFlagKeyFilter, ErpEnvironmentNames.LocalDev),
            ],
            client.Selections);
    }

    [Fact]
    public void Configure_LabelledValue_OverridesTheUnlabelledValue()
    {
        var client = new FakeConfigurationClient()
            .With(ApplicationNameKey, "ERP-AI-Pro")
            .With(ApplicationNameKey, "ERP-AI-Pro (local-dev)", ErpEnvironmentNames.LocalDev)
            .With(ApplicationNameKey, "ERP-AI-Pro (production)", ErpEnvironmentNames.Production);

        var configuration = Load(client, ErpEnvironmentNames.LocalDev);

        Assert.Equal("ERP-AI-Pro (local-dev)", configuration[ApplicationNameKey]);
    }

    [Fact]
    public void Configure_WatchesTheSentinelUnderTheLabelOnly()
    {
        var client = new FakeConfigurationClient().With(AppConfigurationSetup.SentinelKey, "2026-09-20T00:00:00Z");

        Load(client, ErpEnvironmentNames.Production);

        Assert.Equal([(AppConfigurationSetup.SentinelKey, ErpEnvironmentNames.Production)], client.WatchedKeys);
    }

    private static IConfiguration Load(FakeConfigurationClient client, string label) =>
        new ConfigurationBuilder()
            .AddAzureAppConfiguration(options =>
            {
                AppConfigurationSetup.Configure(options, new Uri("https://example.azconfig.io"), label, new FakeTokenCredential(), new AppConfigurationRefreshOptions());
                options.SetClientFactory(new FakeConfigurationClientFactory(client));
            })
            .Build();

    private sealed class FakeTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Tests never request a token.");

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Tests never request a token.");
    }
}
