using System.Net;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Health;

public sealed class HealthEndpointsTests : IClassFixture<ErpApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointsTests(ErpApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/healthz/live")]
    [InlineData("/healthz/ready")]
    public async Task Get_HealthEndpoint_ReturnsHealthy(string path)
    {
        using var response = await _client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
