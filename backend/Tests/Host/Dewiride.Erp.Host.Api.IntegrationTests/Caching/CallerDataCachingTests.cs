using System.Net;
using Dewiride.Erp.BuildingBlocks.Endpoints.Caching;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Caching;

public sealed class CallerDataCachingTests : IClassFixture<CallerDataCachingTests.Fixture>
{
    private const string OwnPath = "/__test/caller/own";

    private const string MissingPath = "/__test/caller/missing";

    private const string ReferencePath = "/__test/caller/reference";

    private readonly HttpClient _client;

    public CallerDataCachingTests(Fixture fixture)
    {
        _client = fixture.Factory.CreateClient().AsUser(TestUsers.Accountant);
    }

    [Fact]
    public async Task Get_CallerDataEndpoint_AnswersAPrivateCacheLifetimeThatVariesByTheSignIn()
    {
        using var response = await _client.GetAsync(new Uri(OwnPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cacheControl = response.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl.Private);
        Assert.Equal(TimeSpan.FromMinutes(10), cacheControl.MaxAge);
        Assert.Equal(["Authorization", "Cookie"], response.Headers.Vary);
    }

    [Fact]
    public async Task Get_ReferenceDataEndpoint_DoesNotVaryByTheSignIn()
    {
        using var response = await _client.GetAsync(new Uri(ReferencePath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Headers.Vary);
    }

    [Theory]
    [InlineData("GET", MissingPath, HttpStatusCode.NotFound)]
    [InlineData("POST", OwnPath, HttpStatusCode.OK)]
    public async Task Send_ResponseThatIsNotASuccessfulCallerDataRead_KeepsNoStoreWithoutVarying(string method, string path, HttpStatusCode status)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(response.Headers.Vary);
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithTestEndpoints(routes =>
        {
            routes.MapMethods(OwnPath, [HttpMethods.Get, HttpMethods.Post], () => Results.Ok(new[] { "own" })).WithCallerDataCaching(TimeSpan.FromMinutes(10));
            routes.MapGet(MissingPath, () => Results.NotFound()).WithCallerDataCaching(TimeSpan.FromMinutes(10));
            routes.MapGet(ReferencePath, () => Results.Ok(new[] { "IN-KA" })).WithReferenceDataCaching(TimeSpan.FromMinutes(10));
        });

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
