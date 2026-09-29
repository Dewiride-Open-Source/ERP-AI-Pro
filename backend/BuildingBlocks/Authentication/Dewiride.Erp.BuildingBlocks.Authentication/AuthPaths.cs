namespace Dewiride.Erp.BuildingBlocks.Authentication;

// The callback paths are registered as redirect URIs of the Entra app registrations by scripts/azure/lib/graph.sh
// (REDIRECT_PATHS); RemoteSignOut is the front-channel logout URL, a separate registration property.
public static class AuthPaths
{
    public const string Prefix = "/api/auth";

    public const string Login = Prefix + "/login";

    public const string Logout = Prefix + "/logout";

    public const string SignInCallback = Prefix + "/signin-oidc";

    public const string SignedOutCallback = Prefix + "/signout-callback-oidc";

    public const string RemoteSignOut = Prefix + "/signout-oidc";

    public const string LoginPage = "/login";

    public const string SignInFailedPage = LoginPage + "?error=sign-in-failed";
}
