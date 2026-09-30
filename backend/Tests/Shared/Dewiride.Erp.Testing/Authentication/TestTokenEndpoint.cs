using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Identity.Client;

namespace Dewiride.Erp.Testing.Authentication;

// MSAL sends every request of a test host through this client instead of one that reaches Entra. The token endpoint
// redeems a code from CodeFor as Entra redeems the code of a person who signed in, with the tokens and client info that
// put the person's account in the token cache, and refuses every other code as Entra refuses an expired one; instance
// discovery answers with the aliases of the public cloud. Anything else fails the request that sent it. ClientInfoFor is
// the client info Entra also posts to the callback with the code, from which Microsoft.Identity.Web adds uid and utid.
public sealed class TestTokenEndpoint : IMsalHttpClientFactory, IDisposable
{
    public const string RefusedCodeError = "invalid_grant";

    public const string RefusedCodeDescription = "AADSTS70008: The provided authorization code or refresh token has expired due to inactivity. Send a new interactive authorization request for this user and resource.";

    private const string CodePrefix = "test-sign-in.";

    private const string InstanceDiscoveryPath = "/common/discovery/instance";

    private readonly ConcurrentQueue<TestTokenRequest> _requests = new();

    private readonly HttpClient _client;

    public TestTokenEndpoint()
    {
        _client = new HttpClient(new Handler(this));
    }

    public IReadOnlyCollection<TestTokenRequest> Requests => _requests;

    public static string CodeFor(TestUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return CodePrefix + user.ObjectId.ToString("D");
    }

    public static string ClientInfoFor(TestUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Encode(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["uid"] = user.ObjectId.ToString("D"),
            ["utid"] = TestIdentityProvider.TenantId,
        });
    }

    public HttpClient GetHttpClient() => _client;

    public void Dispose() => _client.Dispose();

    private async Task<HttpResponseMessage> AnswerAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var address = request.RequestUri ?? throw new InvalidOperationException("MSAL sent a request without an address.");

        if (request.Method == HttpMethod.Get && address.Host == TestIdentityProvider.TokenEndpoint.Host && address.AbsolutePath == InstanceDiscoveryPath)
        {
            return Json(HttpStatusCode.OK, InstanceMetadata());
        }

        if (request.Method == HttpMethod.Post && address.GetLeftPart(UriPartial.Path) == TestIdentityProvider.TokenEndpoint.AbsoluteUri && request.Content is not null)
        {
            var form = QueryHelpers.ParseQuery(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))
                .ToDictionary(field => field.Key, field => field.Value.ToString(), StringComparer.Ordinal);
            _requests.Enqueue(new TestTokenRequest(address, form));

            return UserFor(form.GetValueOrDefault("code")) is { } user
                ? Json(HttpStatusCode.OK, Tokens(user))
                : Json(HttpStatusCode.BadRequest, Refusal());
        }

        throw new InvalidOperationException($"MSAL sent {request.Method} {address.GetLeftPart(UriPartial.Path)}, which the test token endpoint does not serve; no test host may reach Entra.");
    }

    private static TestUser? UserFor(string? code) =>
        code is not null && code.StartsWith(CodePrefix, StringComparison.Ordinal) && Guid.TryParseExact(code[CodePrefix.Length..], "D", out var objectId)
            ? TestUsers.Find(objectId)
            : null;

    private static Dictionary<string, object> InstanceMetadata() => new(StringComparer.Ordinal)
    {
        ["tenant_discovery_endpoint"] = $"{TestIdentityProvider.Instance}{TestIdentityProvider.TenantId}/v2.0/.well-known/openid-configuration",
        ["api-version"] = "1.1",
        ["metadata"] = new[]
        {
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["preferred_network"] = TestIdentityProvider.TokenEndpoint.Host,
                ["preferred_cache"] = "login.windows.net",
                ["aliases"] = new[] { TestIdentityProvider.TokenEndpoint.Host, "login.windows.net", "login.microsoft.com", "sts.windows.net" },
            },
        },
    };

    private static Dictionary<string, object> Tokens(TestUser user)
    {
        var now = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds();
        var idToken = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["aud"] = TestIdentityProvider.ClientId,
            ["iss"] = TestIdentityProvider.Issuer,
            ["iat"] = now,
            ["nbf"] = now,
            ["exp"] = now + 3600,
            ["oid"] = user.ObjectId.ToString("D"),
            ["tid"] = TestIdentityProvider.TenantId,
            ["sub"] = user.ObjectId.ToString("N"),
            ["name"] = user.Name,
            ["preferred_username"] = user.UserName,
            ["ver"] = "2.0",
        };

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["token_type"] = "Bearer",
            ["scope"] = "openid profile offline_access User.Read",
            ["expires_in"] = 3600,
            ["ext_expires_in"] = 3600,
            ["access_token"] = $"test-access-token.{user.ObjectId:N}",
            ["refresh_token"] = $"test-refresh-token.{user.ObjectId:N}",
            ["id_token"] = $"{Encode(new Dictionary<string, object>(StringComparer.Ordinal) { ["alg"] = "none", ["typ"] = "JWT" })}.{Encode(idToken)}.",
            ["client_info"] = ClientInfoFor(user),
        };
    }

    private static Dictionary<string, object> Refusal() => new(StringComparer.Ordinal)
    {
        ["error"] = RefusedCodeError,
        ["error_description"] = RefusedCodeDescription,
        ["error_codes"] = new[] { 70008 },
    };

    private static string Encode(Dictionary<string, object> json) => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(json));

    private static HttpResponseMessage Json(HttpStatusCode status, Dictionary<string, object> body) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private sealed class Handler(TestTokenEndpoint endpoint) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            endpoint.AnswerAsync(request, cancellationToken);
    }
}
