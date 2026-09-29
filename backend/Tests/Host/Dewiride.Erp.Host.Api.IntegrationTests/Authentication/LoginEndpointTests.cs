using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

public sealed class LoginEndpointTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string ReturnPath = "/platform/attachments?view=recent";

    [Fact]
    public async Task Get_LoginAnonymously_RedirectsToTheTenantAuthorizeEndpointWithTheCodeFlow()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(LoginUri(ReturnPath), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal(TestIdentityProvider.AuthorizationEndpoint.GetLeftPart(UriPartial.Path), location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal(TestIdentityProvider.ClientId, Single(query, "client_id"));
        Assert.Equal($"{TestIdentityProvider.WebOrigin}{AuthPaths.SignInCallback}", Single(query, "redirect_uri"));
        Assert.Equal("code", Single(query, "response_type"));
        Assert.Equal("form_post", Single(query, "response_mode"));
        Assert.Equal("S256", Single(query, "code_challenge_method"));
        Assert.False(string.IsNullOrEmpty(Single(query, "code_challenge")));
        Assert.False(string.IsNullOrEmpty(Single(query, "nonce")));
        Assert.Equal(["offline_access", "openid", "profile"], Single(query, "scope").Split(' ').Order(StringComparer.Ordinal));
        Assert.Equal(ReturnPath, ReturnPathOf(Single(query, "state")));
    }

    [Fact]
    public async Task Get_LoginAnonymously_SetsOnlyTheCrossSiteCorrelationAndNonceCookies()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(LoginUri(ReturnPath), TestContext.Current.CancellationToken);

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        Assert.Equal(2, cookies.Count);
        Assert.Contains(cookies, cookie => cookie.StartsWith(".AspNetCore.Correlation.", StringComparison.Ordinal));
        Assert.Contains(cookies, cookie => cookie.StartsWith(".AspNetCore.OpenIdConnect.Nonce.", StringComparison.Ordinal));
        Assert.All(cookies, cookie =>
        {
            var attributes = cookie.ToUpperInvariant();
            Assert.Contains("SECURE", attributes, StringComparison.Ordinal);
            Assert.Contains("SAMESITE=NONE", attributes, StringComparison.Ordinal);
            Assert.Contains("HTTPONLY", attributes, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Get_LoginWithoutAReturnUrl_ReturnsToTheHomePageAfterSignIn(string? returnUrl)
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(LoginUri(returnUrl), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", ReturnPathOf(Single(QueryHelpers.ParseQuery(response.Headers.Location!.Query), "state")));
    }

    [Theory]
    [InlineData("https://evil.example.com/")]
    [InlineData("//evil.example.com")]
    [InlineData("/\\evil.example.com")]
    [InlineData("\\\\evil.example.com")]
    [InlineData("/\t/evil.example.com")]
    [InlineData("~/platform")]
    [InlineData("javascript:alert(1)")]
    [InlineData("platform/attachments")]
    [InlineData("/café")]
    public async Task Get_LoginWithAReturnUrlOffThisSite_AnswersAValidationProblemAndSetsNoCookie(string returnUrl)
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(LoginUri(returnUrl), TestContext.Current.CancellationToken);

        await AssertReturnUrlProblemAsync(response);
    }

    [Fact]
    public async Task Get_LoginWithAReturnUrlOverTheMaximumLength_AnswersAValidationProblemAndSetsNoCookie()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(LoginUri("/" + new string('a', 2048)), TestContext.Current.CancellationToken);

        await AssertReturnUrlProblemAsync(response);
    }

    [Fact]
    public async Task Get_LoginWhenAlreadySignedIn_RedirectsStraightToTheReturnPath()
    {
        using var client = CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(LoginUri(ReturnPath), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(ReturnPath, response.Headers.Location?.OriginalString);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    private HttpClient CreateClient() => factory.CreateClient(new() { AllowAutoRedirect = false });

    private static Uri LoginUri(string? returnUrl) =>
        new(returnUrl is null ? AuthPaths.Login : $"{AuthPaths.Login}?returnUrl={Uri.EscapeDataString(returnUrl)}", UriKind.Relative);

    private static string Single(Dictionary<string, StringValues> query, string name) => Assert.Single(query[name])!;

    private string? ReturnPathOf(string state) =>
        factory.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme)
            .StateDataFormat.Unprotect(state)?.RedirectUri;

    private static async Task AssertReturnUrlProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("/problems/request.invalid", body.RootElement.GetProperty("type").GetString());
        Assert.Equal("request.invalid", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), body.RootElement.GetProperty("traceId").GetString());
        var error = Assert.Single(body.RootElement.GetProperty("errors").EnumerateObject());
        Assert.Equal("returnUrl", error.Name);
    }
}
