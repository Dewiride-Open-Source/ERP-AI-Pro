using System.Net;
using Dewiride.Erp.BuildingBlocks.Endpoints.Caching;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Caching;

public sealed class ReferenceDataCachingTests : IClassFixture<ReferenceDataCachingTests.Fixture>
{
    private const string StatesPath = "/__test/reference/states";

    private const string MissingPath = "/__test/reference/missing";

    private const string UntaggedPath = "/__test/reference/untagged";

    private readonly HttpClient _client;

    public ReferenceDataCachingTests(Fixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task Get_ReferenceDataEndpoint_AnswersAPrivateCacheLifetime()
    {
        using var response = await _client.GetAsync(new Uri(StatesPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cacheControl = response.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl.Private);
        Assert.Equal(TimeSpan.FromMinutes(10), cacheControl.MaxAge);
        Assert.False(cacheControl.NoStore);
    }

    [Theory]
    [InlineData("GET", MissingPath, HttpStatusCode.NotFound)]
    [InlineData("POST", StatesPath, HttpStatusCode.OK)]
    [InlineData("GET", UntaggedPath, HttpStatusCode.OK)]
    public async Task Send_ResponseThatIsNotASuccessfulReferenceDataRead_KeepsNoStore(string method, string path, HttpStatusCode status)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithTestEndpoints(routes =>
        {
            routes.MapMethods(StatesPath, [HttpMethods.Get, HttpMethods.Post], () => Results.Ok(new[] { "IN-KA", "IN-MH" })).WithReferenceDataCaching(TimeSpan.FromMinutes(10));
            routes.MapGet(MissingPath, () => Results.NotFound()).WithReferenceDataCaching(TimeSpan.FromMinutes(10));
            routes.MapGet(UntaggedPath, () => Results.Ok(new[] { "IN-KA" }));
        });

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
