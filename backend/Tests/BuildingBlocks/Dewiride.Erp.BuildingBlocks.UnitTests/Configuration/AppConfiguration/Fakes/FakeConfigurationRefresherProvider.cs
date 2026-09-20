using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.AppConfiguration.Fakes;

internal sealed class FakeConfigurationRefresherProvider(params IConfigurationRefresher[] refreshers) : IConfigurationRefresherProvider
{
    public IEnumerable<IConfigurationRefresher> Refreshers { get; } = refreshers;
}
