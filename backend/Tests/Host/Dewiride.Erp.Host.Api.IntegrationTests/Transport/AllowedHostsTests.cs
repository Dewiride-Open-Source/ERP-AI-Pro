using System.Net;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Transport;

public sealed class AllowedHostsTests : IClassFixture<AllowedHostsTests.Fixture>
{
    private const string AllowedHostsKey = $"{ErpHostOptions.SectionName}:AllowedHosts";

    private readonly HttpClient _client;

    public AllowedHostsTests(Fixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Theory]
    [InlineData("erp.example.com")]
    [InlineData("ERP.EXAMPLE.COM")]
    [InlineData("erp.example.com:8443")]
    [InlineData("localhost")]
    public async Task Get_HostOnTheList_IsServed(string host)
    {
        using var response = await SendAsync(host);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("evil.example.com")]
    [InlineData("example.com")]
    [InlineData("erp.example.com.evil.example")]
    public async Task Get_HostOffTheList_AnswersAHostProblemWithTheCorrelationAndSecurityHeaders(string host)
    {
        using var response = await SendAsync(host);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var correlationId = Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("/problems/request.host-not-allowed", problem!.Type);
        Assert.Equal(ProblemTypes.RequestHostNotAllowed, problem.Extensions["code"]?.ToString());
        Assert.Equal(correlationId, problem.Extensions[ProblemTypes.TraceIdExtension]?.ToString());
    }

    [Fact]
    public async Task Get_AnyHostWithTheDefaultWildcard_IsServed()
    {
        await using var factory = new ErpApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/platform/system-info");
        request.Headers.Host = "anything.example";

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendAsync(string host)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/platform/system-info");
        request.Headers.Host = host;

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithConfiguration(AllowedHostsKey, "erp.example.com; localhost");

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
