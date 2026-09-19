using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Pipeline;

public sealed class SecurityHeadersTests : IClassFixture<ErpApiFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersTests(ErpApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_AnyResponse_CarriesSecurityHeaders()
    {
        using var response = await _client.GetAsync(new Uri("/api/platform/system-info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.Contains("camera=()", response.Headers.GetValues("Permissions-Policy").Single(), StringComparison.Ordinal);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }
}
