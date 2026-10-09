using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Endpoints.OpenApi;
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
    public async Task Get_OpenApiDocument_IsAnOpenApi31DocumentDescribingEveryModuleRoute()
    {
        using var document = await GetAsync();

        Assert.StartsWith("3.1.", document.RootElement.GetProperty("openapi").GetString(), StringComparison.Ordinal);
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/platform/system-info", out _));
        Assert.True(paths.TryGetProperty("/api/platform/system-info/startups", out _));
        Assert.True(paths.TryGetProperty("/api/platform/features", out _));
        Assert.True(paths.TryGetProperty("/api/auth/login", out _));
        Assert.True(paths.TryGetProperty("/api/auth/logout", out _));
    }

    [Fact]
    public async Task Get_OpenApiDocument_CarriesTheApiIdentityAndNoServerEntry()
    {
        using var document = await GetAsync();

        var info = document.RootElement.GetProperty("info");
        Assert.Equal(ErpOpenApiOptions.Title, info.GetProperty("title").GetString());
        Assert.Equal(ErpOpenApiOptions.Version, info.GetProperty("version").GetString());
        var license = info.GetProperty("license");
        Assert.Equal("GNU Lesser General Public License v3.0 only", license.GetProperty("name").GetString());
        Assert.Equal("LGPL-3.0-only", license.GetProperty("identifier").GetString());
        Assert.False(license.TryGetProperty("url", out _));
        Assert.False(document.RootElement.TryGetProperty("servers", out _));
    }

    [Fact]
    public async Task Get_OpenApiDocument_TakesEveryOperationSummaryFromTheModuleThatMapsIt()
    {
        using var document = await GetAsync();

        var operations = document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => (Route: path.Name, Method: operation.Name, Body: operation.Value)))
            .ToList();

        Assert.NotEmpty(operations);
        foreach (var (route, method, body) in operations)
        {
            Assert.True(body.TryGetProperty("summary", out var summary), $"{method} {route} has no summary");
            Assert.False(string.IsNullOrWhiteSpace(summary.GetString()), $"{method} {route} has an empty summary");
            Assert.True(body.TryGetProperty("operationId", out _), $"{method} {route} has no operationId");
        }
    }

    [Fact]
    public async Task Get_OpenApiDocument_DeclaresTheUnauthenticatedProblemOnEveryOperationOutsideTheAuthRoutes()
    {
        using var document = await GetAsync();

        var operations = document.RootElement.GetProperty("paths").EnumerateObject()
            .Where(path => !path.Name.StartsWith(AuthPaths.Prefix + "/", StringComparison.Ordinal))
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => (Route: path.Name, Method: operation.Name, Body: operation.Value)))
            .ToList();

        Assert.Contains(operations, operation => operation.Route == "/api/platform/features");
        Assert.Contains(operations, operation => operation.Route == "/api/platform/attachments/{id}/content");
        foreach (var (route, method, body) in operations)
        {
            Assert.True(body.GetProperty("responses").TryGetProperty("401", out var unauthenticated), $"{method} {route} declares no 401");
            Assert.True(unauthenticated.GetProperty("content").TryGetProperty("application/problem+json", out _), $"{method} {route} declares its 401 without a problem body");
        }
    }

    [Fact]
    public async Task Get_OpenApiDocument_DescribesTheQueryParameterFromTheRequestRecordDocumentation()
    {
        using var document = await GetAsync();

        var parameter = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/platform/system-info/startups")
            .GetProperty("get")
            .GetProperty("parameters")
            .EnumerateArray()
            .Single();

        Assert.Equal("take", parameter.GetProperty("name").GetString());
        Assert.Equal("query", parameter.GetProperty("in").GetString());
        Assert.Contains("between 1 and 100", parameter.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Equal(1, parameter.GetProperty("schema").GetProperty("minimum").GetInt32());
        Assert.Equal(100, parameter.GetProperty("schema").GetProperty("maximum").GetInt32());
    }

    [Fact]
    public async Task Get_OpenApiDocument_DocumentsTheCodeAndTraceIdOfEveryProblem()
    {
        using var document = await GetAsync();

        foreach (var name in new[] { "ProblemDetails", "HttpValidationProblemDetails" })
        {
            var properties = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(name).GetProperty("properties");
            Assert.Equal("string", properties.GetProperty("code").GetProperty("type").GetString());
            Assert.Equal("string", properties.GetProperty("traceId").GetProperty("type").GetString());
        }

        Assert.True(document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("HttpValidationProblemDetails").GetProperty("properties").TryGetProperty("errors", out _));
    }

    private async Task<JsonDocument> GetAsync()
    {
        using var response = await _client.GetAsync(new Uri("/openapi/erp.json", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
