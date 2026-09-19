using System.Net;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.OpenApi;

public sealed class ProductionExposureTests
{
    [Theory]
    [InlineData("/openapi/erp.json")]
    [InlineData("/scalar")]
    public async Task Get_DevelopmentOnlyRoute_IsNotFoundInProduction(string path)
    {
        using var factory = ErpApiFactory.ForEnvironment(Environments.Production);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_ScalarReference_IsExemptFromTheJsonContentSecurityPolicy()
    {
        using var factory = new ErpApiFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = true });

        using var response = await client.GetAsync(new Uri("/scalar", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }
}
