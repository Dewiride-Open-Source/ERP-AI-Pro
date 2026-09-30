using System.Net;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Host.Api.IntegrationTests.OpenApi;

public sealed class ProductionExposureTests
{
    [Theory]
    [InlineData("/openapi/erp.json", true, HttpStatusCode.NotFound)]
    [InlineData("/scalar", true, HttpStatusCode.NotFound)]
    [InlineData("/openapi/erp.json", false, HttpStatusCode.Unauthorized)]
    [InlineData("/scalar", false, HttpStatusCode.Unauthorized)]
    public async Task Get_DevelopmentOnlyRoute_IsNotServedInProduction(string path, bool signedIn, HttpStatusCode status)
    {
        using var factory = ErpApiFactory.ForEnvironment(Environments.Production);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (signedIn)
        {
            request.AsUser(TestUsers.Accountant);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
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
