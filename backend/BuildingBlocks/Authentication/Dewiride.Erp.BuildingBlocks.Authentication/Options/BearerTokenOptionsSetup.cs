using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Options;

// The registration asks for v2.0 access tokens (api.requestedAccessTokenVersion 2), whose aud is the client id and whose
// issuer is the tenant's v2.0 authority, so both are matched exactly, which also refuses a v1.0 token of the tenant, and the
// keys come from that authority's metadata. Claims keep their names, as in the session, and the raw token is not kept.
internal sealed class BearerTokenOptionsSetup(IOptions<EntraSignInOptions> signIn) : IConfigureNamedOptions<JwtBearerOptions>
{
    public const string NameClaimType = "preferred_username";

    public void Configure(JwtBearerOptions options) => Configure(Microsoft.Extensions.Options.Options.DefaultName, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        var issuer = IssuerOf(signIn.Value);
        options.Authority = issuer;
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = false;
        options.SaveToken = false;
        options.TokenValidationParameters.ValidAudience = signIn.Value.ClientId;
        options.TokenValidationParameters.ValidIssuer = issuer;
        options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
        options.TokenValidationParameters.NameClaimType = NameClaimType;
        options.TokenValidationParameters.RoleClaimType = MicrosoftIdentityOptionsSetup.RoleClaimType;
        options.EventsType = typeof(BearerTokenEvents);
    }

    public static string IssuerOf(EntraSignInOptions entra)
    {
        ArgumentNullException.ThrowIfNull(entra);

        return new Uri(new Uri(entra.Instance), $"{entra.TenantId}/v2.0").AbsoluteUri;
    }
}
