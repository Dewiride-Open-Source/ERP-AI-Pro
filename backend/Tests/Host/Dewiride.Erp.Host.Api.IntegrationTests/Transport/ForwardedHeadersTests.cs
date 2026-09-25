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
        var client = await ObserveAsync("127.0.0.1/32", forwardedFor: "203.0.113.7", forwardedProto: "https");

        Assert.Equal(new ObservedClient("203.0.113.7", "https"), client);
    }

    [Fact]
    public async Task Get_FromAPeerOutsideTheKnownNetworks_IgnoresTheForwardedHeaders()
    {
        var client = await ObserveAsync("10.0.0.0/8", forwardedFor: "203.0.113.7", forwardedProto: "https");

        Assert.Equal(new ObservedClient("127.0.0.1", "http"), client);
    }

    [Fact]
    public async Task Get_WithNoKnownNetworks_TrustsNoProxy()
    {
        var client = await ObserveAsync(knownNetwork: null, forwardedFor: "203.0.113.7", forwardedProto: "https");

        Assert.Equal(new ObservedClient("127.0.0.1", "http"), client);
    }

    [Fact]
    public async Task Get_TwoForwardedHops_TakesOnlyTheHopTheTrustedProxyAdded()
    {
        var client = await ObserveAsync("127.0.0.1/32", forwardedFor: "198.51.100.1, 203.0.113.7", forwardedProto: null);

        Assert.Equal(new ObservedClient("203.0.113.7", "http"), client);
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

    private static async Task<ObservedClient?> ObserveAsync(string? knownNetwork, string forwardedFor, string? forwardedProto)
    {
        await using var factory = new ErpApiFactory();
        if (knownNetwork is not null)
        {
            factory.WithConfiguration(NetworksKey, knownNetwork);
        }

        factory
            .WithTestEndpoints(routes => routes.MapGet(ClientPath, (HttpContext context) =>
                new ObservedClient(context.Connection.RemoteIpAddress?.ToString(), context.Request.Scheme)))
            .WithKestrel();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, ClientPath);
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        if (forwardedProto is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", forwardedProto);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<ObservedClient>(TestContext.Current.CancellationToken);
    }

    private sealed record ObservedClient(string? Remote, string Scheme);
}
