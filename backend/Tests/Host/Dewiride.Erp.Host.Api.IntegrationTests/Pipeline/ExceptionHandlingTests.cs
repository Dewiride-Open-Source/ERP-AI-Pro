using System.Net;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Pipeline;

public sealed class ExceptionHandlingTests : IClassFixture<ErpApiFactory>
{
    private readonly HttpClient _client;

    public ExceptionHandlingTests(ErpApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_ThrowingRoute_ReturnsProblemDetailsWithSecurityHeaders()
    {
        using var response = await _client.GetAsync(new Uri(ErpApiFactory.ThrowingPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("An unexpected error occurred.", problem.Title);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
        Assert.DoesNotContain("Deliberate failure", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
    }
}
