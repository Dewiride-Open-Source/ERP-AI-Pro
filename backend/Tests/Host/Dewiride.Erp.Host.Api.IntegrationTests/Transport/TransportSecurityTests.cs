using System.Net;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Transport;

public sealed class TransportSecurityTests
{
    private const string SchemePath = "/__test/scheme";

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
    public async Task Get_OverPlainHttpWithAnHttpsPortKnown_IsServedWithoutARedirectAnHstsPolicyOrAServerHeader(string environment)
    {
        await using var factory = ErpApiFactory.ForEnvironment(environment).WithConfiguration("https_port", "443").WithKestrel();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/platform/system-info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Uri.UriSchemeHttp, response.RequestMessage!.RequestUri!.Scheme);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
        Assert.False(response.Headers.Contains("Server"));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Get_OverHttpsForwardedByATrustedProxyForAPublicHost_SendsNoHstsPolicy(string environment)
    {
        await using var factory = ErpApiFactory.ForEnvironment(environment)
            .WithConfiguration($"{ErpHostOptions.SectionName}:KnownNetworks:0", "127.0.0.1/32")
            .WithTestEndpoints(routes => routes.MapGet(SchemePath, (HttpContext context) => context.Request.IsHttps))
            .WithKestrel();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, SchemePath);
        request.Headers.Host = "erp.example.com";
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.7");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await response.Content.ReadFromJsonAsync<bool>(TestContext.Current.CancellationToken));
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    private static string Single(HttpResponseMessage response, string header) => Assert.Single(response.Headers.GetValues(header));
}
