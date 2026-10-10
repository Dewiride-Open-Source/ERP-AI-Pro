using Dewiride.Erp.BuildingBlocks.Authentication;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication;

public sealed class AuthPathsTests
{
    [Fact]
    public void SignInPaths_Always_AreTheLoginBothCallbacksTheFrontChannelSignOutAndTheLogout()
    {
        Assert.Equal(
            ["/api/auth/login", "/api/auth/logout", "/api/auth/signin-oidc", "/api/auth/signout-callback-oidc", "/api/auth/signout-oidc"],
            AuthPaths.SignInPaths.Order(StringComparer.Ordinal));
    }
}
