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
// put the person's account in the token cache, for a persona of TestUsers or a person a test host admitted, redeems a code
// from CodeWithoutClientInfoFor with the same tokens and no client info, and refuses every other code as Entra refuses an
// expired one. It redeems a refresh token it issued for the scopes MSAL asks, as Entra does when the API calls Microsoft
// Graph for the person, unless a test made the person's consent missing, and answers the client credentials grant, signed
// for with a client assertion, with an application token of Microsoft Graph's .default scope and nobody as its subject;
// instance discovery answers with the aliases of the public cloud. Anything else fails the request that sent it. ScopeOf,
// SubjectOf and IsApplicationToken tell what an access token it issued grants and whose it is, which Graph reads from the
// token itself. ClientInfoFor is that client info, from whose account Microsoft.Identity.Web adds uid and utid; the sign-in
// asks Entra for no client info on the callback, and Microsoft.Identity.Web ignores one posted there.
public sealed class TestTokenEndpoint : IMsalHttpClientFactory, IDisposable
{
    public const string RefusedCodeError = "invalid_grant";

    public const string RefusedCodeDescription = "AADSTS70008: The provided authorization code or refresh token has expired due to inactivity. Send a new interactive authorization request for this user and resource.";

    public const string ConsentRequiredDescription = "AADSTS65001: The user or administrator has not consented to use the application. Send an interactive authorization request for this user and resource.";

    public const string ApplicationScope = "https://graph.microsoft.com/.default";

    private const string SignInScope = "openid profile offline_access User.Read";

    private const string ClientCredentialsGrant = "client_credentials";

    private const string ClientAssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

    private const string ApplicationTokenPrefix = "test-application-token.";

    private const string CodePrefix = "test-sign-in.";

    private const string CodeWithoutClientInfoPrefix = "test-sign-in-without-client-info.";

    private const string ClientInfoField = "client_info";

    private const string RefreshTokenPrefix = "test-refresh-token.";

    private const string RefreshTokenGrant = "refresh_token";

    private const string InstanceDiscoveryPath = "/common/discovery/instance";

    private readonly ConcurrentQueue<TestTokenRequest> _requests = new();

    private readonly ConcurrentQueue<string> _issuedTokens = new();

    private readonly ConcurrentDictionary<string, (Guid? Subject, string Scope)> _accessTokens = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<Guid, TestUser> _admitted = new();

    private readonly ConcurrentDictionary<Guid, bool> _consentMissing = new();

    private readonly HttpClient _client;

    private long _issuedAccessTokens;

    public TestTokenEndpoint()
    {
        _client = new HttpClient(new Handler(this));
    }

    public IReadOnlyCollection<TestTokenRequest> Requests => _requests;

    public IReadOnlyCollection<string> IssuedTokens => _issuedTokens;

    public static string CodeFor(TestUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return CodePrefix + user.ObjectId.ToString("D");
    }

    public static string CodeWithoutClientInfoFor(TestUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return CodeWithoutClientInfoPrefix + user.ObjectId.ToString("D");
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

    public void Admit(TestUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        _admitted[user.ObjectId] = user;
    }

    public TestUser? Find(Guid objectId) => TestUsers.Find(objectId) ?? _admitted.GetValueOrDefault(objectId);

    // Entra refuses the refresh token of a person whose consent the API lacks for the scopes it asks, which MSAL reports as a
    // sign-in interaction the person must complete.
    public void WithholdConsent(TestUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        _consentMissing[user.ObjectId] = true;
    }

    public string? ScopeOf(string accessToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);

        return _accessTokens.TryGetValue(accessToken, out var issued) ? issued.Scope : null;
    }

    public Guid? SubjectOf(string accessToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);

        return _accessTokens.TryGetValue(accessToken, out var issued) ? issued.Subject : null;
    }

    public bool IsApplicationToken(string accessToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);

        return _accessTokens.TryGetValue(accessToken, out var issued) && issued.Subject is null;
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

            return form.GetValueOrDefault("grant_type") switch
            {
                RefreshTokenGrant => Refresh(form.GetValueOrDefault("refresh_token"), form.GetValueOrDefault("scope")),
                ClientCredentialsGrant => ApplicationToken(form),
                _ => Redeem(form.GetValueOrDefault("code")),
            };
        }

        throw new InvalidOperationException($"MSAL sent {request.Method} {address.GetLeftPart(UriPartial.Path)}, which the test token endpoint does not serve; no test host may reach Entra.");
    }

    private HttpResponseMessage Redeem(string? code)
    {
        if (UserFor(code, CodePrefix) is { } user)
        {
            return Json(HttpStatusCode.OK, Tokens(user, SignInScope));
        }

        if (UserFor(code, CodeWithoutClientInfoPrefix) is { } userWithoutClientInfo)
        {
            var tokens = Tokens(userWithoutClientInfo, SignInScope);
            tokens.Remove(ClientInfoField);

            return Json(HttpStatusCode.OK, tokens);
        }

        return Json(HttpStatusCode.BadRequest, Refusal(RefusedCodeError, RefusedCodeDescription, 70008));
    }

    private HttpResponseMessage Refresh(string? refreshToken, string? scope)
    {
        if (RefreshedUserFor(refreshToken) is not { } user || string.IsNullOrWhiteSpace(scope))
        {
            return Json(HttpStatusCode.BadRequest, Refusal(RefusedCodeError, RefusedCodeDescription, 70008));
        }

        return _consentMissing.ContainsKey(user.ObjectId)
            ? Json(HttpStatusCode.BadRequest, Refusal(RefusedCodeError, ConsentRequiredDescription, 65001))
            : Json(HttpStatusCode.OK, Tokens(user, scope));
    }

    private HttpResponseMessage ApplicationToken(Dictionary<string, string> form)
    {
        if (form.GetValueOrDefault("client_assertion_type") != ClientAssertionType || string.IsNullOrEmpty(form.GetValueOrDefault("client_assertion")))
        {
            return Json(HttpStatusCode.Unauthorized, Refusal("invalid_client", "AADSTS7000216: 'client_assertion', 'client_secret' or 'request' is required for the 'client_credentials' grant type.", 7000216));
        }

        if (form.GetValueOrDefault("scope")?.Split(' ', StringSplitOptions.RemoveEmptyEntries) is not [ApplicationScope])
        {
            return Json(HttpStatusCode.BadRequest, Refusal("invalid_scope", "AADSTS1002012: The provided value for scope is not valid. Client credential flows must have a scope value with /.default suffixed to the resource identifier (application ID URI).", 1002012));
        }

        var token = $"{ApplicationTokenPrefix}{Interlocked.Increment(ref _issuedAccessTokens)}";
        _issuedTokens.Enqueue(token);
        _accessTokens[token] = (null, ApplicationScope);

        return Json(HttpStatusCode.OK, new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["token_type"] = "Bearer",
            ["expires_in"] = 3600,
            ["ext_expires_in"] = 3600,
            ["access_token"] = token,
        });
    }

    private TestUser? UserFor(string? code, string prefix) =>
        code is not null && code.StartsWith(prefix, StringComparison.Ordinal) && Guid.TryParseExact(code[prefix.Length..], "D", out var objectId)
            ? Find(objectId)
            : null;

    private TestUser? RefreshedUserFor(string? refreshToken) =>
        refreshToken is not null && refreshToken.StartsWith(RefreshTokenPrefix, StringComparison.Ordinal) && Guid.TryParseExact(refreshToken[RefreshTokenPrefix.Length..], "N", out var objectId)
            ? Find(objectId)
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

    private Dictionary<string, object> Tokens(TestUser user, string scope)
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
            ["sid"] = user.EntraSessionId,
            ["name"] = user.Name,
            ["preferred_username"] = user.UserName,
            ["login_hint"] = user.LoginHint,
            ["ver"] = "2.0",
        };

        string[] tokens =
        [
            $"test-access-token.{user.ObjectId:N}.{Interlocked.Increment(ref _issuedAccessTokens)}",
            $"{RefreshTokenPrefix}{user.ObjectId:N}",
            $"{Encode(new Dictionary<string, object>(StringComparer.Ordinal) { ["alg"] = "none", ["typ"] = "JWT" })}.{Encode(idToken)}.",
        ];
        foreach (var token in tokens)
        {
            _issuedTokens.Enqueue(token);
        }

        _accessTokens[tokens[0]] = (user.ObjectId, scope);

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["token_type"] = "Bearer",
            ["scope"] = scope,
            ["expires_in"] = 3600,
            ["ext_expires_in"] = 3600,
            ["access_token"] = tokens[0],
            ["refresh_token"] = tokens[1],
            ["id_token"] = tokens[2],
            [ClientInfoField] = ClientInfoFor(user),
        };
    }

    private static Dictionary<string, object> Refusal(string error, string description, int errorCode) => new(StringComparer.Ordinal)
    {
        ["error"] = error,
        ["error_description"] = description,
        ["error_codes"] = new[] { errorCode },
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
