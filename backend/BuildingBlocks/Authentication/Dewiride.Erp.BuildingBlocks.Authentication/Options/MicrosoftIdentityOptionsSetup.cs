using Dewiride.Erp.BuildingBlocks.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Options;

// Microsoft.Identity.Web builds the OpenID Connect handler options from these, and chains its own handlers onto Events,
// so the handler options themselves are never configured directly.
internal sealed class MicrosoftIdentityOptionsSetup(IOptions<EntraSignInOptions> signIn, SignInEvents events) : IConfigureNamedOptions<MicrosoftIdentityOptions>
{
    public const string RoleClaimType = "roles";

    public void Configure(MicrosoftIdentityOptions options) => Configure(Microsoft.Extensions.Options.Options.DefaultName, options);

    public void Configure(string? name, MicrosoftIdentityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!string.Equals(name, OpenIdConnectDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        var entra = signIn.Value;
        options.Instance = entra.Instance;
        options.TenantId = entra.TenantId;
        options.ClientId = entra.ClientId;
        options.ClientCredentials = [new CredentialDescription { SourceType = CredentialSource.Base64Encoded, Base64EncodedValue = entra.ClientCertificate }];

        options.CallbackPath = AuthPaths.SignInCallback;
        options.SignedOutCallbackPath = AuthPaths.SignedOutCallback;
        options.RemoteSignOutPath = AuthPaths.RemoteSignOut;
        options.SignedOutRedirectUri = AuthPaths.LoginPage;

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.FormPost;
        options.UsePkce = true;

        // Entra advertises no pushed authorization endpoint today; disabling it keeps the flow fixed if it ever does.
        options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
        options.SaveTokens = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.RoleClaimType = RoleClaimType;

        // The identity provider posts the callback from its own site, so these cookies must travel cross-site.
        foreach (var cookie in new[] { options.CorrelationCookie, options.NonceCookie })
        {
            cookie.HttpOnly = true;
            cookie.SecurePolicy = CookieSecurePolicy.Always;
            cookie.SameSite = SameSiteMode.None;
        }

        options.Events.OnRedirectToIdentityProvider = events.RedirectToIdentityProvider;
        options.Events.OnRedirectToIdentityProviderForSignOut = events.RedirectToIdentityProviderForSignOut;
        options.Events.OnRemoteFailure = events.RemoteFailure;
    }
}
