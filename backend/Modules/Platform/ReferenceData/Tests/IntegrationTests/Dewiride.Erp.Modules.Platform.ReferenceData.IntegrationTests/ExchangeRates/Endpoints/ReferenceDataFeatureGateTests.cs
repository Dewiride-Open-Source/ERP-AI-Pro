using System.Net;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.ExchangeRates.Endpoints;

public sealed class ReferenceDataFeatureGateTests(ReferenceDataFeatureGateTests.Fixture fixture) : IClassFixture<ReferenceDataFeatureGateTests.Fixture>
{
    private const string FeatureDisabled = "feature.disabled";

    [Theory]
    [InlineData("GET", "/exchange-rates")]
    [InlineData("GET", "/exchange-rates/{id}")]
    [InlineData("POST", "/exchange-rates")]
    [InlineData("POST", "/exchange-rates/imports")]
    [InlineData("PUT", "/exchange-rates/{id}")]
    [InlineData("DELETE", "/exchange-rates/{id}")]
    [InlineData("GET", "/countries")]
    [InlineData("GET", "/indian-states")]
    [InlineData("GET", "/currencies")]
    [InlineData("GET", "/financial-years?from=2026-27&to=2026-27")]
    public async Task Request_ModuleDisabled_AnswersFeatureDisabled(string method, string suffix)
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);
        var path = "/api/platform/reference-data" + suffix.Replace("{id}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal);
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var problem = await ExchangeRatesApi.AssertProblemAsync(response, HttpStatusCode.NotFound, FeatureDisabled);
        Assert.Contains(ReferenceDataModule.FeatureFlag, problem.Detail, StringComparison.Ordinal);
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithFeature(ReferenceDataModule.FeatureFlag, enabled: false);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
