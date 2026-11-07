using System.Net;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.ParameterSets.Endpoints;

public sealed class StatutoryParametersFeatureGateTests(StatutoryParametersFeatureGateTests.Fixture fixture) : IClassFixture<StatutoryParametersFeatureGateTests.Fixture>
{
    private const string FeatureDisabled = "feature.disabled";

    [Theory]
    [InlineData("")]
    [InlineData("/gst.central-tax-rates")]
    public async Task Get_ModuleDisabled_AnswersFeatureDisabled(string suffix)
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await client.GetAsync(ParameterSetsApi.Path(suffix), TestContext.Current.CancellationToken);

        var problem = await ParameterSetsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, FeatureDisabled);
        Assert.Contains(StatutoryParametersModule.FeatureFlag, problem.Detail, StringComparison.Ordinal);
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithFeature(StatutoryParametersModule.FeatureFlag, enabled: false);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
