using System.Net;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Transport;

public sealed class TransportSecurityTests
{
    [Theory]
    [InlineData("Development", "/api/platform/system-info", HttpStatusCode.OK)]
    [InlineData("Production", "/api/platform/system-info", HttpStatusCode.OK)]
    [InlineData("Development", "/api/platform/does-not-exist", HttpStatusCode.NotFound)]
    [InlineData("Production", "/api/platform/does-not-exist", HttpStatusCode.NotFound)]
    public async Task Get_OverARealListener_CarriesTheApiHeaders(string environment, string path, HttpStatusCode status)
    {
        await using var factory = ErpApiFactory.ForEnvironment(environment).WithKestrel();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Single(response, "Referrer-Policy"));
        Assert.Contains("camera=()", Single(response, "Permissions-Policy"), StringComparison.Ordinal);
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", Single(response, "Content-Security-Policy"));
        Assert.Equal("same-origin", Single(response, "Cross-Origin-Resource-Policy"));
        Assert.Equal("same-origin", Single(response, "Cross-Origin-Opener-Policy"));
        Assert.Equal("none", Single(response, "X-Permitted-Cross-Domain-Policies"));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Get_OverPlainHttp_IsServedWithoutARedirectAnHstsPolicyOrAServerHeader(string environment)
    {
        await using var factory = ErpApiFactory.ForEnvironment(environment).WithKestrel();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/platform/system-info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Uri.UriSchemeHttp, response.RequestMessage!.RequestUri!.Scheme);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
        Assert.False(response.Headers.Contains("Server"));
    }

    private static string Single(HttpResponseMessage response, string header) => Assert.Single(response.Headers.GetValues(header));
}
