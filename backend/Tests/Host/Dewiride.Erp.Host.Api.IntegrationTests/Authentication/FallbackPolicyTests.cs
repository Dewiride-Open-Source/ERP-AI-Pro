using System.Net;
using System.Text.RegularExpressions;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

public sealed partial class FallbackPolicyTests(FallbackPolicyTests.Fixture fixture) : IClassFixture<FallbackPolicyTests.Fixture>
{
    private const string SignedInPath = "/__test/signed-in";

    private const string AdministratorsPath = "/__test/administrators";

    private const string UnknownPath = "/api/platform/does-not-exist";

    [Fact]
    public async Task Send_AnonymousRequestToEveryRouteThatIsNotAnonymous_AnswersUnauthenticatedWithoutALocation()
    {
        using var client = fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var protectedRoutes = fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [HttpMethods.Get])
                .Select(method => (Method: method, Path: SamplePath(endpoint.RoutePattern.RawText!))))
            .ToList();
        Assert.Contains((HttpMethods.Post, "/api/auth/logout"), protectedRoutes);

        foreach (var (method, path) in protectedRoutes)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            await AssertUnauthenticatedAsync(response, $"{method} {path}");
        }
    }

    [Fact]
    public async Task Get_UnknownRouteAnonymously_AnswersUnauthenticated()
    {
        using var client = fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        using var response = await client.GetAsync(new Uri(UnknownPath, UriKind.Relative), TestContext.Current.CancellationToken);

        await AssertUnauthenticatedAsync(response, UnknownPath);
    }

    [Fact]
    public async Task Get_UnknownRouteSignedIn_AnswersNotFound()
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(new Uri(UnknownPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemTypes.ResourceNotFound, (await ReadProblemAsync(response)).Extensions["code"]?.ToString());
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.MethodNotAllowed)]
    public async Task Post_RouteThatOnlyAllowsGet_AnswersMethodNotAllowedOnlyToASignedInPerson(bool signedIn, HttpStatusCode status)
    {
        using var client = fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/platform/system-info");
        if (signedIn)
        {
            request.AsUser(TestUsers.Accountant);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Get_ProtectedRouteSignedIn_Serves()
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(new Uri(SignedInPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_RoleRestrictedRouteForAPersonWithoutTheRole_AnswersForbiddenWithoutALocation()
    {
        using var client = fixture.Factory.CreateClient(new() { AllowAutoRedirect = false }).AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(new Uri(AdministratorsPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("/problems/request.forbidden", problem.Type);
        Assert.Equal(ProblemTypes.RequestForbidden, problem.Extensions["code"]?.ToString());
        Assert.Equal(AdministratorsPath, problem.Instance);
    }

    [Fact]
    public async Task Get_RoleRestrictedRouteForAPersonWithTheRole_Serves()
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await client.GetAsync(new Uri(AdministratorsPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/xml")]
    public async Task Get_ProtectedRouteAnonymouslyForAClientThatAcceptsNoJson_AnswersUnauthenticatedAsPlainText(string accept)
    {
        using var client = fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, SignedInPath);
        request.Headers.Accept.ParseAdd(accept);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    private static async Task AssertUnauthenticatedAsync(HttpResponseMessage response, string route)
    {
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{route} answered {(int)response.StatusCode}");
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var problem = await ReadProblemAsync(response);
        Assert.Equal("/problems/request.unauthenticated", problem.Type);
        Assert.Equal(ProblemTypes.RequestUnauthenticated, problem.Extensions["code"]?.ToString());
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions[ProblemTypes.TraceIdExtension]?.ToString());
    }

    private static async Task<ProblemDetails> ReadProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);

        return problem;
    }

    private static string SamplePath(string pattern) =>
        RouteParameter().Replace(pattern, match => match.Value.Contains(":guid", StringComparison.Ordinal) ? Guid.CreateVersion7().ToString("D") : "sample");

    [GeneratedRegex(@"\{[^}]+\}", RegexOptions.CultureInvariant)]
    private static partial Regex RouteParameter();

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithTestEndpoints(routes =>
        {
            routes.MapGet(SignedInPath, () => Results.Ok());
            routes.MapGet(AdministratorsPath, () => Results.Ok()).RequireAuthorization(policy => policy.RequireRole(TestUsers.AdminRole));
        });

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
