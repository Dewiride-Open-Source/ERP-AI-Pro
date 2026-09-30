using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Dewiride.Erp.BuildingBlocks.Idempotency.Persistence;
using Dewiride.Erp.BuildingBlocks.Modules.Features;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Features;

public sealed class FeatureGateOrderTests : IClassFixture<FeatureGateOrderTests.Fixture>
{
    private const string Flag = "Erp.Modules.Platform.SystemInfo";

    private const string GatedOrdersPath = "/__test/gated-orders";

    private const string OpenOrdersPath = "/__test/open-orders";

    private readonly Fixture _fixture;

    private readonly HttpClient _client;

    public FeatureGateOrderTests(Fixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient().AsUser(TestUsers.Accountant);
    }

    [Fact]
    public async Task Post_IdempotentEndpointOfADisabledModuleWithoutAKey_AnswersFeatureDisabledBeforeTheKeyCheck()
    {
        using var response = await PostAsync(_client, GatedOrdersPath, key: null);

        await AssertCodeAsync(response, HttpStatusCode.NotFound, "feature.disabled");
    }

    [Fact]
    public async Task Post_IdempotentEndpointOfADisabledModuleTwiceWithOneKey_NeverClaimsTheKey()
    {
        var key = Guid.CreateVersion7().ToString("D");

        using var first = await PostAsync(_client, GatedOrdersPath, key);
        using var second = await PostAsync(_client, GatedOrdersPath, key);

        await AssertCodeAsync(first, HttpStatusCode.NotFound, "feature.disabled");
        await AssertCodeAsync(second, HttpStatusCode.NotFound, "feature.disabled");
        Assert.False(second.Headers.Contains(IdempotencyKeyHeader.ReplayedName));
    }

    [Theory]
    [InlineData(GatedOrdersPath, false)]
    [InlineData(GatedOrdersPath, true)]
    [InlineData(OpenOrdersPath, false)]
    [InlineData(OpenOrdersPath, true)]
    public async Task Post_ProtectedIdempotentEndpointAnonymously_AnswersUnauthenticatedBeforeTheFeatureGateAndTheKeyCheck(string path, bool withKey)
    {
        using var anonymous = _fixture.Factory.CreateClient();
        var key = Guid.CreateVersion7();

        using var response = await PostAsync(anonymous, path, withKey ? key.ToString("D") : null);

        await AssertCodeAsync(response, HttpStatusCode.Unauthorized, ProblemTypes.RequestUnauthenticated);
        Assert.False(await IsKeyStoredAsync(key));
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { item = "pen" }) };
        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation(IdempotencyKeyHeader.Name, key);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task AssertCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }

    private async Task<bool> IsKeyStoredAsync(Guid key)
    {
        using var scope = _fixture.Factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>().Records.AnyAsync(record => record.Key == key, TestContext.Current.CancellationToken);
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory()
            .WithFeature(Flag, enabled: false)
            .WithTestEndpoints(routes =>
            {
                routes.MapPost(GatedOrdersPath, () => Results.Created()).RequireIdempotencyKey().RequireFeature(Flag);
                routes.MapPost(OpenOrdersPath, () => Results.Created()).RequireIdempotencyKey();
            });

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
