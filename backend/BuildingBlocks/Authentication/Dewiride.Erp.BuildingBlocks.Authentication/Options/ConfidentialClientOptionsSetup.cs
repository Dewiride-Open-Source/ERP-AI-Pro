using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Options;

// MSAL sends this redirect URI when it redeems the code, and Entra refuses a redemption whose URI differs from the one the
// authorization request carried; without it MSAL would build the URI from the host of the first request and keep it.
internal sealed class ConfidentialClientOptionsSetup(IOptions<EntraSignInOptions> signIn) : IConfigureNamedOptions<ConfidentialClientApplicationOptions>
{
    public void Configure(ConfidentialClientApplicationOptions options) => Configure(Microsoft.Extensions.Options.Options.DefaultName, options);

    public void Configure(string? name, ConfidentialClientApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.Equals(name, OpenIdConnectDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            options.RedirectUri = signIn.Value.WebOrigin + AuthPaths.SignInCallback;
        }
    }
}
