using System.Text.Json;
using Dewiride.Erp.Host.Api.IntegrationTests.BearerTokens;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.OpenApi;

namespace Dewiride.Erp.Host.Api.IntegrationTests.OpenApi;

public sealed class OpenApiSecurityTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string Session = """[{"SessionCookie":[]}]""";

    private const string SessionWithAntiforgeryToken = """[{"SessionCookie":[],"AntiforgeryToken":[]}]""";

    private const string Anonymous = "[]";

    private const string AnonymousOrSessionWithAntiforgeryToken = """[{},{"SessionCookie":[],"AntiforgeryToken":[]}]""";

    private const string AnonymousChangesPath = "/__test/anonymous-changes";

    public static TheoryData<string, string, string> Requirements => new()
    {
        { "/api/auth/login", "get", Anonymous },
        { "/api/auth/logout", "post", SessionWithAntiforgeryToken },
        { "/api/auth/antiforgery", "get", Session },
        { "/api/auth/me", "get", Session },
        { "/api/auth/session", "get", Session },
        { "/api/auth/session", "post", SessionWithAntiforgeryToken },
        { "/api/platform/features", "get", Session },
        { "/api/platform/system-info", "get", Session },
        { "/api/platform/attachments", "get", Session },
        { "/api/platform/attachments", "post", SessionWithAntiforgeryToken },
        { "/api/platform/attachments/{id}", "delete", SessionWithAntiforgeryToken },
        { "/api/platform/attachments/{id}/content", "get", Session },
    };

    [Fact]
    public async Task Document_Always_DeclaresTheSessionCookieAndAntiforgeryHeaderAndNoBearerSchemeWithoutABearerRoute()
    {
        using var document = await DocumentAsync(factory);

        var schemes = document.RootElement.GetProperty("components").GetProperty("securitySchemes");
        Assert.Equal(["SessionCookie", "AntiforgeryToken"], schemes.EnumerateObject().Select(scheme => scheme.Name));
        AssertScheme(schemes.GetProperty("SessionCookie"), ("type", "apiKey"), ("name", "__Host-erp-session"), ("in", "cookie"));
        AssertScheme(schemes.GetProperty("AntiforgeryToken"), ("type", "apiKey"), ("name", "X-XSRF-TOKEN"), ("in", "header"));
    }

    [Fact]
    public async Task Document_EveryOperation_DeclaresItsSecurityNamingOnlyDeclaredSchemes()
    {
        using var document = await DocumentAsync(factory);

        var declared = document.RootElement.GetProperty("components").GetProperty("securitySchemes").EnumerateObject().Select(scheme => scheme.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var (route, method, operation) in Operations(document))
        {
            Assert.True(operation.TryGetProperty("security", out var security), $"{method} {route} declares no security");
            var requirements = security.EnumerateArray().ToList();
            for (var index = 0; index < requirements.Count; index++)
            {
                var names = requirements[index].EnumerateObject().Select(scheme => scheme.Name).ToList();
                Assert.All(names, name => Assert.Contains(name, declared));
                Assert.True(names.Count > 0 || (index == 0 && requirements.Count == 2), $"{method} {route} carries an empty requirement other than the optional one before the session");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Requirements))]
    public async Task Document_Operation_RequiresWhatTheApiChecks(string route, string method, string expected)
    {
        using var document = await DocumentAsync(factory);

        var security = document.RootElement.GetProperty("paths").GetProperty(route).GetProperty(method).GetProperty("security");

        Assert.Equal(expected, JsonSerializer.Serialize(security));
    }

    [Fact]
    public async Task Document_RouteThatTakesBearerTokens_ListsOneBearerAlternativePerScopeOrApplicationRoleAndDeclaresTheScheme()
    {
        await using var withBearerRoute = BearerTokenRoutes.Factory();

        using var document = await DocumentAsync(withBearerRoute);

        var operation = document.RootElement.GetProperty("paths").GetProperty(BearerTokenRoutes.Path);
        Assert.Equal(
            $$"""[{"BearerToken":["{{BearerTokenRoutes.ReadScope}}"]},{"BearerToken":["{{BearerTokenRoutes.IntegrationRole}}"]}]""",
            JsonSerializer.Serialize(operation.GetProperty("post").GetProperty("security")));
        AssertScheme(document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("BearerToken"), ("type", "http"), ("scheme", "bearer"), ("bearerFormat", "JWT"));
        Assert.Equal(Session, JsonSerializer.Serialize(document.RootElement.GetProperty("paths").GetProperty(BearerTokenRoutes.SessionPath).GetProperty("get").GetProperty("security")));
    }

    [Fact]
    public async Task Document_AnonymousRouteThatChangesData_AllowsAnonymousCallersOrTheSessionCookieWithItsAntiforgeryToken()
    {
        await using var withAnonymousChanges = new ErpApiFactory().WithTestEndpoints(routes => routes.MapPost(AnonymousChangesPath, () => Results.NoContent()).AllowAnonymous());

        using var document = await DocumentAsync(withAnonymousChanges);

        var security = document.RootElement.GetProperty("paths").GetProperty(AnonymousChangesPath).GetProperty("post").GetProperty("security");
        Assert.Equal(AnonymousOrSessionWithAntiforgeryToken, JsonSerializer.Serialize(security));
    }

    [Fact]
    public async Task Document_FrontChannelSignOut_IsNotDescribed()
    {
        using var document = await DocumentAsync(factory);

        Assert.False(document.RootElement.GetProperty("paths").TryGetProperty("/api/auth/signout-oidc", out _));
    }

    private static async Task<JsonDocument> DocumentAsync(ErpApiFactory host) =>
        JsonDocument.Parse(await OpenApiSnapshot.SerializeAsync(host.Services, TestContext.Current.CancellationToken));

    private static IEnumerable<(string Route, string Method, JsonElement Operation)> Operations(JsonDocument document) =>
        document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => (path.Name, operation.Name, operation.Value)));

    private static void AssertScheme(JsonElement scheme, params (string Name, string Value)[] members)
    {
        Assert.Equal(
            members.Select(member => member.Name).Append("description").Order(StringComparer.Ordinal),
            scheme.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal));
        foreach (var (name, value) in members)
        {
            Assert.Equal(value, scheme.GetProperty(name).GetString());
        }

        Assert.False(string.IsNullOrWhiteSpace(scheme.GetProperty("description").GetString()));
    }
}
