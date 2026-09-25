using System.Net;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.Testing;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Transport;

public sealed class ForwardedHeadersTests
{
    private const string ClientPath = "/__test/client";

    private const string NetworksKey = $"{ErpHostOptions.SectionName}:KnownNetworks:0";

    [Fact]
    public async Task Get_FromAPeerInsideAKnownNetwork_TakesTheForwardedClientAndScheme()
    {
        var client = await ObserveAsync("127.0.0.1/32", ("X-Forwarded-For", "203.0.113.7"), ("X-Forwarded-Proto", "https"));

        Assert.Equal("203.0.113.7", client.Remote);
        Assert.Equal("https", client.Scheme);
    }

    [Fact]
    public async Task Get_FromAPeerOutsideTheKnownNetworks_IgnoresTheForwardedHeaders()
    {
        var client = await ObserveAsync("10.0.0.0/8", ("X-Forwarded-For", "203.0.113.7"), ("X-Forwarded-Proto", "https"));

        Assert.Equal("127.0.0.1", client.Remote);
        Assert.Equal("http", client.Scheme);
    }

    [Fact]
    public async Task Get_WithNoKnownNetworks_TrustsNoProxy()
    {
        var client = await ObserveAsync(knownNetwork: null, ("X-Forwarded-For", "203.0.113.7"), ("X-Forwarded-Proto", "https"));

        Assert.Equal("127.0.0.1", client.Remote);
        Assert.Equal("http", client.Scheme);
    }

    [Fact]
    public async Task Get_TwoForwardedHops_TakesOnlyTheHopTheTrustedProxyAdded()
    {
        var client = await ObserveAsync("127.0.0.1/32", ("X-Forwarded-For", "198.51.100.1, 203.0.113.7"));

        Assert.Equal("203.0.113.7", client.Remote);
    }

    [Fact]
    public async Task Get_TwoForwardedHopsThatAreBothTrusted_StopsAfterOneHop()
    {
        var client = await ObserveAsync("127.0.0.0/8", ("X-Forwarded-For", "198.51.100.1, 127.0.0.2"));

        Assert.Equal("127.0.0.2", client.Remote);
    }

    [Fact]
    public async Task Get_ForwardedHostFromATrustedPeer_KeepsTheRequestHost()
    {
        var client = await ObserveAsync("127.0.0.1/32", ("X-Forwarded-For", "203.0.113.7"), ("X-Forwarded-Host", "evil.example"));

        Assert.Equal("203.0.113.7", client.Remote);
        Assert.NotEqual("evil.example", client.Host);
        Assert.StartsWith("127.0.0.1:", client.Host, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-a-network")]
    [InlineData("172.28.0.5/16")]
    public async Task Start_WithAKnownNetworkThatIsNotANetwork_FailsNamingTheEntry(string network)
    {
        await using var factory = new ErpApiFactory().WithConfiguration(NetworksKey, network);

        var failure = Record.Exception(() => factory.CreateClient());

        var validation = Assert.IsType<OptionsValidationException>(failure);
        Assert.Contains(NetworksKey, validation.Message, StringComparison.Ordinal);
        Assert.Contains(network, validation.Message, StringComparison.Ordinal);
    }

    private static async Task<ObservedClient> ObserveAsync(string? knownNetwork, params (string Name, string Value)[] headers)
    {
        await using var factory = new ErpApiFactory();
        if (knownNetwork is not null)
        {
            factory.WithConfiguration(NetworksKey, knownNetwork);
        }

        factory
            .WithTestEndpoints(routes => routes.MapGet(ClientPath, (HttpContext context) =>
                new ObservedClient(context.Connection.RemoteIpAddress?.ToString(), context.Request.Scheme, context.Request.Host.Value)))
            .WithKestrel();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, ClientPath);
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var observed = await response.Content.ReadFromJsonAsync<ObservedClient>(TestContext.Current.CancellationToken);
        Assert.NotNull(observed);

        return observed;
    }

    private sealed record ObservedClient(string? Remote, string Scheme, string? Host);
}
