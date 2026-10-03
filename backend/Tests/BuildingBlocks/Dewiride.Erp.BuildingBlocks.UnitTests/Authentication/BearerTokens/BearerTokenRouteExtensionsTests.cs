using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.BearerTokens;

public sealed class BearerTokenRouteExtensionsTests
{
    private static readonly BearerTokenAccess Access = new(["Erp.Read"], ["Erp.Integration"]);

    [Fact]
    public async Task RequireBearerToken_OnARoute_AddsAPolicyNamingTheBearerSchemeAloneAndTheRouteMetadata()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapGet("/reports", () => Results.NoContent()).RequireBearerToken(Access);

        var endpoint = Assert.Single(EndpointsOf(app));

        var policy = Assert.Single(endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>());
        Assert.Equal(["Bearer"], policy.AuthenticationSchemes);
        Assert.Same(Access, Assert.IsType<BearerTokenAccessRequirement>(Assert.Single(policy.Requirements, requirement => requirement is BearerTokenAccessRequirement)).Access);
        Assert.Same(Access, endpoint.Metadata.GetMetadata<BearerTokenRouteMetadata>()?.Access);
    }

    [Fact]
    public async Task RequireBearerToken_OnAGroupAndOneOfItsRoutes_FailsWhenTheRouteIsBuilt()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapGroup("/reports").RequireBearerToken(Access).MapGet("/export", () => Results.NoContent()).RequireBearerToken(new BearerTokenAccess(["Erp.Export"], []));

        var exception = Assert.Throws<InvalidOperationException>(() => EndpointsOf(app));

        Assert.Contains("calls RequireBearerToken more than once; a route takes one BearerTokenAccess.", exception.Message, StringComparison.Ordinal);
    }

    private static List<Endpoint> EndpointsOf(IEndpointRouteBuilder routes) => [.. routes.DataSources.SelectMany(source => source.Endpoints)];
}
