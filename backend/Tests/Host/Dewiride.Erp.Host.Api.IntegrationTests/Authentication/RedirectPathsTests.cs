using System.Text.RegularExpressions;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

// The Entra app registrations get their redirect URIs, and the store its identity settings, from the graph.sh script; a
// path or key that differs from the API's would make Entra refuse the sign-in or leave the API without its settings.
public sealed partial class RedirectPathsTests
{
    private static readonly string GraphScript = File.ReadAllText(RepositoryPaths.Combine("scripts", "azure", "lib", "graph.sh"));

    [Fact]
    public void RegisteredRedirectPaths_AreTheSignInAndSignedOutCallbacksOnly()
    {
        var match = RedirectPaths().Match(GraphScript);
        Assert.True(match.Success, "scripts/azure/lib/graph.sh declares no readonly REDIRECT_PATHS array.");

        var registered = match.Groups["paths"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal([AuthPaths.SignInCallback, AuthPaths.SignedOutCallback], registered);
        Assert.DoesNotContain(AuthPaths.RemoteSignOut, registered);
    }

    [Theory]
    [InlineData("IDENTITY_TENANT_ID_KEY", ErpApiFactory.IdentityTenantIdKey)]
    [InlineData("IDENTITY_CLIENT_ID_KEY", ErpApiFactory.IdentityClientIdKey)]
    [InlineData("IDENTITY_WEB_ORIGIN_KEY", ErpApiFactory.IdentityWebOriginKey)]
    public void RegistrationScriptKeys_AreTheSettingsTheApiBinds(string variable, string key)
    {
        var match = Regex.Match(GraphScript, $"^readonly {variable}='(?<key>[^']+)'\r?$", RegexOptions.Multiline | RegexOptions.CultureInvariant);

        Assert.True(match.Success, $"scripts/azure/lib/graph.sh declares no readonly {variable}.");
        Assert.Equal(key, match.Groups["key"].Value);
    }

    [GeneratedRegex(@"^readonly REDIRECT_PATHS=\((?<paths>[^)]*)\)\r?$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex RedirectPaths();
}
