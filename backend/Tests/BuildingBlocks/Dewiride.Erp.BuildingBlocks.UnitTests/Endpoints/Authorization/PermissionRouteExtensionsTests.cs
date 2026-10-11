using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.Authorization;

public sealed class PermissionRouteExtensionsTests
{
    private const string Permission = "finance.sales.invoices.issue";

    [Fact]
    public async Task RequirePermission_OnARoute_AddsAPolicyRequiringASignedInCallerAndThePermissionAndDeclaresForbidden()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapPost("/invoices", () => TypedResults.NoContent()).RequirePermission(Permission);

        var endpoint = Assert.Single(EndpointsOf(app));

        var policy = Assert.Single(endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>());
        Assert.Empty(policy.AuthenticationSchemes);
        Assert.Single(policy.Requirements.OfType<DenyAnonymousAuthorizationRequirement>());
        Assert.Equal(Permission, Assert.Single(policy.Requirements.OfType<PermissionRequirement>()).Permission);
        Assert.Equal(Permission, endpoint.Metadata.GetMetadata<PermissionMetadata>()?.Permission);
        Assert.Contains(endpoint.Metadata.OfType<IProducesResponseTypeMetadata>(), response => response.StatusCode == StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task RequirePermission_OnAGroupAndOneOfItsRoutes_FailsWhenTheRouteIsBuilt()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        app.MapGroup("/invoices").RequirePermission(Permission).MapDelete("/{id}", () => TypedResults.NoContent()).RequirePermission("finance.sales.invoices.void");

        var exception = Assert.Throws<InvalidOperationException>(() => EndpointsOf(app));

        Assert.Contains("calls RequirePermission more than once; a route requires one permission.", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RequirePermission_BlankPermission_Throws(string permission)
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();

        Assert.Throws<ArgumentException>(() => app.MapGet("/invoices", () => TypedResults.NoContent()).RequirePermission(permission));
    }

    private static List<Endpoint> EndpointsOf(IEndpointRouteBuilder routes) => [.. routes.DataSources.SelectMany(source => source.Endpoints)];
}
