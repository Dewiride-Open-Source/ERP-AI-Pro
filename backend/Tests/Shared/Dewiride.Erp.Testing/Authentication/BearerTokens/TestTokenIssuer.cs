using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Dewiride.Erp.Testing.Authentication.BearerTokens;

// Signs access tokens shaped like the v2.0 tokens Entra issues for the API's app registration, with one RSA key per test
// process, and gives the bearer scheme of every test host a fixed configuration holding that key, so the handler never
// downloads the tenant's metadata or keys and no test reaches Entra. A person's token comes through a client application
// and carries scp; an application's token carries no scp, its roles, idtyp app and a sub equal to its oid.
public static class TestTokenIssuer
{
    public const string KeyId = "erp-test-access-token-key";

    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(1);

    private static readonly JsonWebTokenHandler Handler = new();

    private static readonly Lazy<SigningCredentials> Credentials = new(() => CreateSigningCredentials(KeyId));

    public static string V1Issuer => $"https://sts.windows.net/{TestIdentityProvider.TenantId}/";

    public static SigningCredentials CreateSigningCredentials(string keyId) =>
        new(new RsaSecurityKey(RSA.Create(2048)) { KeyId = keyId }, SecurityAlgorithms.RsaSha256);

    public static void Configure(JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configuration = new OpenIdConnectConfiguration { Issuer = TestIdentityProvider.Issuer };
        configuration.SigningKeys.Add(Credentials.Value.Key);
        options.Configuration = configuration;
        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
    }

    public static string ForPerson(TestUser person, TestApplication client, params string[] scopes) => Issue(PersonClaims(person, client, scopes));

    public static string ForApplication(TestApplication application, params string[] roles) => Issue(ApplicationClaims(application, roles));

    public static Dictionary<string, object> PersonClaims(TestUser person, TestApplication client, IEnumerable<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(scopes);

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["aud"] = TestIdentityProvider.ClientId,
            ["iss"] = TestIdentityProvider.Issuer,
            ["tid"] = TestIdentityProvider.TenantId,
            ["oid"] = person.ObjectId.ToString("D"),
            ["sub"] = $"pairwise-{person.ObjectId:N}",
            ["azp"] = client.ClientId.ToString("D"),
            ["azpacr"] = "0",
            ["scp"] = string.Join(' ', scopes),
            ["name"] = person.Name,
            ["preferred_username"] = person.UserName,
            ["roles"] = person.Roles.ToArray(),
            ["ver"] = "2.0",
        };
    }

    public static Dictionary<string, object> ApplicationClaims(TestApplication application, IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(roles);

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["aud"] = TestIdentityProvider.ClientId,
            ["iss"] = TestIdentityProvider.Issuer,
            ["tid"] = TestIdentityProvider.TenantId,
            ["oid"] = application.ObjectId.ToString("D"),
            ["sub"] = application.ObjectId.ToString("D"),
            ["azp"] = application.ClientId.ToString("D"),
            ["azpacr"] = "2",
            ["idtyp"] = "app",
            ["roles"] = roles.ToArray(),
            ["ver"] = "2.0",
        };
    }

    public static string Issue(IReadOnlyDictionary<string, object> claims, DateTimeOffset? issuedAt = null, TimeSpan? lifetime = null, SigningCredentials? signedWith = null) =>
        Handler.CreateToken(Payload(claims, issuedAt, lifetime), signedWith ?? Credentials.Value);

    public static string IssueUnsigned(IReadOnlyDictionary<string, object> claims) => Handler.CreateToken(Payload(claims, issuedAt: null, lifetime: null));

    private static string Payload(IReadOnlyDictionary<string, object> claims, DateTimeOffset? issuedAt, TimeSpan? lifetime)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var issued = issuedAt ?? TimeProvider.System.GetUtcNow();
        var payload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["iat"] = issued.ToUnixTimeSeconds(),
            ["nbf"] = issued.ToUnixTimeSeconds(),
            ["exp"] = (issued + (lifetime ?? DefaultLifetime)).ToUnixTimeSeconds(),
        };
        foreach (var (name, value) in claims)
        {
            payload[name] = value;
        }

        return JsonSerializer.Serialize(payload);
    }
}
