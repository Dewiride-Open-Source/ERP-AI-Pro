using System.Text.Json;
using System.Text.RegularExpressions;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.Graph;
using Dewiride.Erp.BuildingBlocks.Authentication.OpenIdConnect;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

// The Entra app registrations get their redirect URIs, front-channel logout URL, optional claims and Microsoft Graph
// permissions, and the store its identity settings, from the graph.sh script; a path, claim, permission or key that differs
// from the API's would make Entra refuse the sign-in or the directory's token, call a route the API does not serve, leave
// out the hint the sign-out sends, or leave the API without its settings.
public sealed partial class RedirectPathsTests
{
    private const string GraphResource = "https://graph.microsoft.com/";

    private static readonly string GraphScript = File.ReadAllText(RepositoryPaths.Combine("scripts", "azure", "lib", "graph.sh"));

    [Fact]
    public void RegisteredRedirectPaths_AreTheSignInAndSignedOutCallbacksOnly()
    {
        var match = RedirectPaths().Match(GraphScript);
        Assert.True(match.Success, "scripts/azure/lib/graph.sh declares no readonly REDIRECT_PATHS array.");

        var registered = match.Groups["paths"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal([AuthPaths.SignInCallback, AuthPaths.SignedOutCallback], registered);
    }

    [Fact]
    public void RegisteredGraphPermissions_IncludeTheDelegatedPermissionTheDirectoryAsksFor()
    {
        var match = GraphScopeValues().Match(GraphScript);
        Assert.True(match.Success, "scripts/azure/lib/graph.sh declares no readonly GRAPH_SCOPE_VALUES array.");

        var registered = match.Groups["scopes"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains(registered, scope => string.Equals($"{GraphResource}{scope}", MicrosoftGraph.ReadBasicProfilesScope, StringComparison.Ordinal));
    }

    [Fact]
    public void RegisteredFrontChannelSignOutPath_IsTheFrontChannelSignOutRouteOfTheApi()
    {
        Assert.Equal(AuthPaths.FrontChannelSignOut, ReadonlyValue("FRONT_CHANNEL_SIGN_OUT_PATH"));
    }

    [Fact]
    public void RegisteredOptionalClaims_AreExactlyTheLoginHintOfIdTokens()
    {
        using var claims = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.Combine(ReadonlyValue("OPTIONAL_CLAIMS_FILE").Split('/'))));

        var tokenTypes = claims.RootElement.EnumerateObject().ToDictionary(tokenType => tokenType.Name, tokenType => tokenType.Value.EnumerateArray().ToList(), StringComparer.Ordinal);

        Assert.Equal(["accessToken", "idToken", "saml2Token"], tokenTypes.Keys.Order(StringComparer.Ordinal));
        Assert.Empty(tokenTypes["accessToken"]);
        Assert.Empty(tokenTypes["saml2Token"]);
        var loginHint = Assert.Single(tokenTypes["idToken"]);
        Assert.Equal(SignInEvents.LoginHintClaim, loginHint.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, loginHint.GetProperty("source").ValueKind);
        Assert.False(loginHint.GetProperty("essential").GetBoolean());
        Assert.Empty(loginHint.GetProperty("additionalProperties").EnumerateArray());
    }

    [Theory]
    [InlineData("IDENTITY_TENANT_ID_KEY", ErpApiFactory.IdentityTenantIdKey)]
    [InlineData("IDENTITY_CLIENT_ID_KEY", ErpApiFactory.IdentityClientIdKey)]
    [InlineData("IDENTITY_WEB_ORIGIN_KEY", ErpApiFactory.IdentityWebOriginKey)]
    public void RegistrationScriptKeys_AreTheSettingsTheApiBinds(string variable, string key)
    {
        Assert.Equal(key, ReadonlyValue(variable));
    }

    private static string ReadonlyValue(string variable)
    {
        var match = Regex.Match(GraphScript, $"^readonly {variable}='(?<value>[^']+)'\r?$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"scripts/azure/lib/graph.sh declares no readonly {variable}.");

        return match.Groups["value"].Value;
    }

    [GeneratedRegex(@"^readonly REDIRECT_PATHS=\((?<paths>[^)]*)\)\r?$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex RedirectPaths();

    [GeneratedRegex(@"^readonly GRAPH_SCOPE_VALUES=\((?<scopes>[^)]*)\)\r?$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex GraphScopeValues();
}
