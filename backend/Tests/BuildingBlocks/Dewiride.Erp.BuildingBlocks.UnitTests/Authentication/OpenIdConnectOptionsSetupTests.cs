using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication;

public sealed class OpenIdConnectOptionsSetupTests
{
    [Fact]
    public void PostConfigure_OpenIdConnectScheme_KeepsTheIssuerClaimAndEveryOtherDefaultAction()
    {
        var options = new OpenIdConnectOptions();
        var defaults = options.ClaimActions.Select(action => action.ClaimType).ToList();

        new OpenIdConnectOptionsSetup().PostConfigure(OpenIdConnectDefaults.AuthenticationScheme, options);

        Assert.Contains("iss", defaults);
        Assert.Equal(defaults.Where(claimType => claimType != "iss"), options.ClaimActions.Select(action => action.ClaimType));
    }

    [Fact]
    public void PostConfigure_AnotherScheme_LeavesItsDefaultActions()
    {
        var options = new OpenIdConnectOptions();
        var defaults = options.ClaimActions.Select(action => action.ClaimType).ToList();

        new OpenIdConnectOptionsSetup().PostConfigure("Other", options);

        Assert.Equal(defaults, options.ClaimActions.Select(action => action.ClaimType));
    }
}
