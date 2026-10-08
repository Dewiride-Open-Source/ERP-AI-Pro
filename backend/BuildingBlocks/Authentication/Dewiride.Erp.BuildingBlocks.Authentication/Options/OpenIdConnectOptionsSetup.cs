using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Options;

// The handler deletes the iss claim of the id token by default, and Microsoft.Identity.Web only ever adds claim actions to
// these options, in a configure step of its own, so keeping the claim takes a post-configure step. A session records the
// issuer with its Entra session id (EntraSessions), and the front-channel sign-out names both.
internal sealed class OpenIdConnectOptionsSetup : IPostConfigureOptions<OpenIdConnectOptions>
{
    public void PostConfigure(string? name, OpenIdConnectOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.Equals(name, OpenIdConnectDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            options.ClaimActions.Remove(JwtRegisteredClaimNames.Iss);
        }
    }
}
