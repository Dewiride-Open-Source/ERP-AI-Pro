using Azure.Data.AppConfiguration;
using Microsoft.Extensions.Azure;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.AppConfiguration.Fakes;

internal sealed class FakeConfigurationClientFactory(FakeConfigurationClient client) : IAzureClientFactory<ConfigurationClient>
{
    public ConfigurationClient CreateClient(string name) => client;
}
