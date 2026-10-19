using Azure.Core;
using Azure.Data.AppConfiguration;
using Dewiride.Erp.BuildingBlocks.Configuration.AppConfiguration;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.AppConfiguration.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.AppConfiguration;

public sealed class AppConfigurationSetupTests
{
    private const string ApplicationNameKey = "Erp:Platform:Host:ApplicationName";
    private const string ConnectionStringKey = "Erp:Platform:Database:ConnectionString";
    private const string ClientCertificateKey = "Erp:Platform:Identity:ClientCertificate";
    private const string FeatureFlagKeyFilter = ".appconfig.featureflag/*";
    private const string SecretName = "Erp--Platform--Example";
    private static readonly Uri Vault = new("https://kv-example.vault.azure.net/");
    private static readonly Uri Secret = new(Vault, $"secrets/{SecretName}");

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

    [Fact]
    public void Configure_NamedReferences_LoadsAnUnnamedReferenceEmptyWithoutOpeningKeyVault()
    {
        var client = new FakeConfigurationClient().WithReference(ClientCertificateKey, Secret, ErpEnvironmentNames.Production);
        var secrets = new FakeSecretClient(Vault);

        var configuration = Load(client, ErpEnvironmentNames.Production, new HashSet<string> { ConnectionStringKey }, secrets);

        Assert.Equal(string.Empty, configuration[ClientCertificateKey]);
        Assert.Empty(secrets.Requested);
    }

    [Fact]
    public void Configure_NamedReferences_OpensTheNamedReference()
    {
        var client = new FakeConfigurationClient().WithReference(ConnectionStringKey, Secret, ErpEnvironmentNames.Production);
        var secrets = new FakeSecretClient(Vault);

        var configuration = Load(client, ErpEnvironmentNames.Production, new HashSet<string> { ConnectionStringKey }, secrets);

        Assert.Equal(FakeSecretClient.ValueOf(SecretName), configuration[ConnectionStringKey]);
        Assert.Equal([SecretName], secrets.Requested);
    }

    [Fact]
    public void Configure_NoNamedReferences_OpensEveryReference()
    {
        var client = new FakeConfigurationClient().WithReference(ClientCertificateKey, Secret, ErpEnvironmentNames.Production);
        var secrets = new FakeSecretClient(Vault);

        var configuration = Load(client, ErpEnvironmentNames.Production, keyVaultReferences: null, secrets);

        Assert.Equal(FakeSecretClient.ValueOf(SecretName), configuration[ClientCertificateKey]);
        Assert.Equal([SecretName], secrets.Requested);
    }

    [Fact]
    public void WithoutUnnamedReference_PlainSetting_ReturnsTheSettingUnchanged()
    {
        var setting = new ConfigurationSetting(ApplicationNameKey, "ERP-AI-Pro");

        Assert.Same(setting, AppConfigurationSetup.WithoutUnnamedReference(setting, new HashSet<string>()));
    }

    private static IConfiguration Load(
        FakeConfigurationClient client,
        string label,
        IReadOnlySet<string>? keyVaultReferences = null,
        FakeSecretClient? secrets = null) =>
        new ConfigurationBuilder()
            .AddAzureAppConfiguration(options =>
            {
                AppConfigurationSetup.Configure(
                    options,
                    new Uri("https://example.azconfig.io"),
                    label,
                    new FakeTokenCredential(),
                    new AppConfigurationRefreshOptions(),
                    keyVaultReferences);
                options.SetClientFactory(new FakeConfigurationClientFactory(client));
                if (secrets is not null)
                {
                    options.ConfigureKeyVault(keyVault => keyVault.Register(secrets));
                }
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
