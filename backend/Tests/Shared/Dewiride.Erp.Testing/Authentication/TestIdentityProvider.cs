using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Dewiride.Erp.Testing.Authentication;

// The handler would otherwise download the discovery document of the tenant on its first challenge; a fixed document
// keeps every test host off the network, so no test can reach Entra.
public static class TestIdentityProvider
{
    public const string TenantId = "5d7c3b9a-1e2f-4a6b-8c0d-9e8f7a6b5c4d";

    public const string ClientId = "0e9d8c7b-6a5f-4e3d-8c1b-0a9f8e7d6c5b";

    public const string WebOrigin = "https://erp.example.com";

    public const string Instance = "https://login.microsoftonline.com/";

    public static string Issuer => $"{Instance}{TenantId}/v2.0";

    public static Uri AuthorizationEndpoint { get; } = new($"{Instance}{TenantId}/oauth2/v2.0/authorize");

    public static Uri TokenEndpoint { get; } = new($"{Instance}{TenantId}/oauth2/v2.0/token");

    public static Uri EndSessionEndpoint { get; } = new($"{Instance}{TenantId}/oauth2/v2.0/logout");

    public static void Configure(OpenIdConnectOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configuration = new OpenIdConnectConfiguration
        {
            Issuer = Issuer,
            AuthorizationEndpoint = AuthorizationEndpoint.AbsoluteUri,
            TokenEndpoint = TokenEndpoint.AbsoluteUri,
            EndSessionEndpoint = EndSessionEndpoint.AbsoluteUri,
        };
        options.Configuration = configuration;
        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
    }
}
