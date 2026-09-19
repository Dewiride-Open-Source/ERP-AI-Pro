using System.Net;
using System.Text.Json;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.OpenApi;

public sealed class OpenApiDocumentTests : IClassFixture<ErpApiFactory>
{
    private readonly HttpClient _client;

    public OpenApiDocumentTests(ErpApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_OpenApiDocument_DescribesEveryModuleRoute()
    {
        using var response = await _client.GetAsync(new Uri("/openapi/erp.json", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/platform/system-info", out _));
        Assert.StartsWith("3.", document.RootElement.GetProperty("openapi").GetString(), StringComparison.Ordinal);
    }
}
