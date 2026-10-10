namespace Dewiride.Erp.BuildingBlocks.Authentication;

// The callback paths are registered as redirect URIs of the Entra app registrations by scripts/azure/lib/graph.sh
// (REDIRECT_PATHS), and the front-channel sign-out path as the front-channel logout URL of every registration whose web
// origin is https (FRONT_CHANNEL_SIGN_OUT_PATH).
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

    public const string FrontChannelSignOut = Prefix + "/signout-oidc";

    public const string LoginPage = "/login";

    public const string SignInFailedPage = LoginPage + "?error=sign-in-failed";

    public const string AccountDeactivatedPage = LoginPage + "?error=account-deactivated";

    public const string SignedOutPage = LoginPage + "?reason=signed-out";

    public static IReadOnlyCollection<string> SignInPaths { get; } = [Login, SignInCallback, SignedOutCallback, FrontChannelSignOut, Logout];
}
