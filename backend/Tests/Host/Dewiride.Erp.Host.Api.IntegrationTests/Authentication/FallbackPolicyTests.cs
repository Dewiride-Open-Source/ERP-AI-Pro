using System.Net;
using System.Text.RegularExpressions;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

public sealed partial class FallbackPolicyTests(FallbackPolicyTests.Fixture fixture) : IClassFixture<FallbackPolicyTests.Fixture>
{
    private const string SignedInPath = "/__test/signed-in";

    private const string AdministratorsPath = "/__test/administrators";

    private const string UnknownPath = "/api/platform/does-not-exist";

    private const string SelectedEndpointHeader = "X-Test-Selected-Endpoint";

    private const string NoEndpoint = "none";

    [Fact]
    public async Task Send_AnonymousRequestToEveryRouteThatIsNotAnonymous_AnswersUnauthenticatedWithoutALocation()
    {
        using var client = fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var protectedRoutes = fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [HttpMethods.Get])
                .Select(method => (Endpoint: endpoint, Method: method, Path: SamplePath(endpoint.RoutePattern.RawText!))))
            .ToList();
        Assert.Contains(protectedRoutes, route => route.Method == HttpMethods.Post && route.Path == "/api/auth/session");

        foreach (var (endpoint, method, path) in protectedRoutes)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            var selected = Uri.UnescapeDataString(Assert.Single(response.Headers.GetValues(SelectedEndpointHeader)));
            Assert.True(
                selected == DescriptionOf(endpoint),
                $"{method} {path} reached '{selected}' instead of '{DescriptionOf(endpoint)}'; give SamplePath a value the route's constraints accept.");
            await AssertUnauthenticatedAsync(response, $"{method} {path}");
        }
    }

    [Theory]
    [InlineData("GET", "/api/platform/system-info")]
    [InlineData("GET", "/api/platform/system-info/startups")]
    [InlineData("GET", "/api/platform/features")]
    [InlineData("GET", "/api/platform/attachments")]
    [InlineData("GET", "/api/platform/attachments/policy")]
    [InlineData("POST", "/api/platform/attachments")]
    [InlineData("GET", "/api/platform/attachments/{id}")]
    [InlineData("DELETE", "/api/platform/attachments/{id}")]
    [InlineData("POST", "/api/platform/attachments/{id}/download-links")]
    [InlineData("GET", "/api/platform/attachments/{id}/content?link=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Send_PlatformRouteWithoutASession_AnswersUnauthenticated(string method, string route)
    {
        using var client = fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(new HttpMethod(method), route.Replace("{id}", Guid.CreateVersion7().ToString("D"), StringComparison.Ordinal));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(DescriptionOf(EndpointFor(method, route)), Uri.UnescapeDataString(Assert.Single(response.Headers.GetValues(SelectedEndpointHeader))));
        await AssertUnauthenticatedAsync(response, $"{method} {route}");
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

    private static string DescriptionOf(RouteEndpoint endpoint) =>
        $"{string.Join(',', endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])} {endpoint.RoutePattern.RawText} {endpoint.DisplayName}";

    private RouteEndpoint EndpointFor(string method, string route) =>
        Assert.Single(
            fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>(),
            endpoint => (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []).Contains(method, StringComparer.Ordinal)
                && RouteParameter().Replace(endpoint.RoutePattern.RawText!.TrimEnd('/'), "{id}") == route.Split('?')[0]);

    private static string SamplePath(string pattern) =>
        RouteParameter().Replace(pattern, match => match.Value.Contains(":guid", StringComparison.Ordinal) ? Guid.CreateVersion7().ToString("D") : "sample");

    [GeneratedRegex(@"\{[^}]+\}", RegexOptions.CultureInvariant)]
    private static partial Regex RouteParameter();

    public sealed class Fixture : IAsyncDisposable
    {
        private readonly ErpApiFactory _root = new ErpApiFactory().WithTestEndpoints(routes =>
        {
            routes.MapGet(SignedInPath, () => Results.Ok());
            routes.MapGet(AdministratorsPath, () => Results.Ok()).RequireAuthorization(policy => policy.RequireRole(AppRoles.Administrator));
        });

        public Fixture()
        {
            Factory = _root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddTransient<IStartupFilter, SelectedEndpointStartupFilter>()));
        }

        public WebApplicationFactory<Program> Factory { get; }

        public async ValueTask DisposeAsync()
        {
            await Factory.DisposeAsync();
            await _root.DisposeAsync();
        }
    }

    private sealed class SelectedEndpointStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, pipeline) =>
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers[SelectedEndpointHeader] = context.GetEndpoint() is RouteEndpoint endpoint ? Uri.EscapeDataString(DescriptionOf(endpoint)) : NoEndpoint;

                    return Task.CompletedTask;
                });

                return pipeline(context);
            });
            next(app);
        };
    }
}
