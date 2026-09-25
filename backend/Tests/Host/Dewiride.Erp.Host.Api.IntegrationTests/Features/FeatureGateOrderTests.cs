using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Dewiride.Erp.BuildingBlocks.Modules.Features;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Features;

public sealed class FeatureGateOrderTests : IClassFixture<FeatureGateOrderTests.Fixture>
{
    private const string Flag = "Erp.Modules.Platform.SystemInfo";

    private const string GatedOrdersPath = "/__test/gated-orders";

    private readonly HttpClient _client;

    public FeatureGateOrderTests(Fixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task Post_IdempotentEndpointOfADisabledModuleWithoutAKey_AnswersFeatureDisabledBeforeTheKeyCheck()
    {
        using var response = await PostAsync(key: null);

        await AssertFeatureDisabledAsync(response);
    }

    [Fact]
    public async Task Post_IdempotentEndpointOfADisabledModuleTwiceWithOneKey_NeverClaimsTheKey()
    {
        var key = Guid.CreateVersion7().ToString("D");

        using var first = await PostAsync(key);
        using var second = await PostAsync(key);

        await AssertFeatureDisabledAsync(first);
        await AssertFeatureDisabledAsync(second);
        Assert.False(second.Headers.Contains(IdempotencyKeyHeader.ReplayedName));
    }

    private async Task<HttpResponseMessage> PostAsync(string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GatedOrdersPath) { Content = JsonContent.Create(new { item = "pen" }) };
        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation(IdempotencyKeyHeader.Name, key);
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task AssertFeatureDisabledAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("feature.disabled", body.RootElement.GetProperty("code").GetString());
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory()
            .WithFeature(Flag, enabled: false)
            .WithTestEndpoints(routes => routes.MapPost(GatedOrdersPath, () => Results.Created()).RequireIdempotencyKey().RequireFeature(Flag));

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
