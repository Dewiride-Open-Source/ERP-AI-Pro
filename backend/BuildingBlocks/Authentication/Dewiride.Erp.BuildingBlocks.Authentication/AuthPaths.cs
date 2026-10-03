namespace Dewiride.Erp.BuildingBlocks.Authentication;

// The callback paths are registered as redirect URIs of the Entra app registrations by scripts/azure/lib/graph.sh
// (REDIRECT_PATHS).
public static class AuthPaths
{
    public const string Prefix = "/api/auth";

    public const string Login = Prefix + "/login";

    public const string Logout = Prefix + "/logout";

    public const string Antiforgery = Prefix + "/antiforgery";

    public const string Me = Prefix + "/me";

    public const string Session = Prefix + "/session";

    public const string SignInCallback = Prefix + "/signin-oidc";

    public const string SignedOutCallback = Prefix + "/signout-callback-oidc";

    public const string LoginPage = "/login";

    public const string SignInFailedPage = LoginPage + "?error=sign-in-failed";
}
