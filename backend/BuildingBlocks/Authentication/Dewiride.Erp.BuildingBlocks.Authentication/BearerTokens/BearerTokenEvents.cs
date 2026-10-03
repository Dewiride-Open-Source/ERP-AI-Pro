using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Microsoft.Identity.Web;
using Microsoft.Net.Http.Headers;

namespace Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;

// While the flag is off the scheme signs nothing in and names itself in no challenge, as if it were not registered. A token
// signed for the registration is also refused unless it is a v2.0 access token of the tenant held by a person or an
// application, which leaves out the id tokens the sign-in receives for the same audience. A refused token is answered
// invalid_token without the reason (RFC 6750, section 3.1), and every refusal carries a problem like the session cookie's.
internal sealed class BearerTokenEvents(IOptions<EntraSignInOptions> signIn, IProblemDetailsService problemDetails) : JwtBearerEvents
{
    public const string NoTokenChallenge = JwtBearerDefaults.AuthenticationScheme;

    public const string InvalidTokenChallenge = $"{NoTokenChallenge} error=\"invalid_token\"";

    public const string InsufficientScopeChallenge = $"{NoTokenChallenge} error=\"insufficient_scope\"";

    public const string NotAnAccessTokenMessage = "The token is not a v2.0 access token issued in this tenant to a person or an application.";

    public override async Task MessageReceived(MessageReceivedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!await IsEnabledAsync(context.HttpContext).ConfigureAwait(false))
        {
            context.NoResult();
        }
    }

    public override Task TokenValidated(TokenValidatedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Principal is not { } token || !IsAccessTokenOfTheTenant(token))
        {
            context.Fail(NotAnAccessTokenMessage);
        }

        return Task.CompletedTask;
    }

    public override async Task Challenge(JwtBearerChallengeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.HandleResponse();
        if (await IsEnabledAsync(context.HttpContext).ConfigureAwait(false))
        {
            context.Response.Headers.Append(HeaderNames.WWWAuthenticate, context.AuthenticateFailure is null ? NoTokenChallenge : InvalidTokenChallenge);
        }

        await AuthenticationProblems.WriteUnauthenticatedAsync(context.HttpContext, problemDetails).ConfigureAwait(false);
    }

    public override Task Forbidden(ForbiddenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.Headers.Append(HeaderNames.WWWAuthenticate, InsufficientScopeChallenge);

        return AuthenticationProblems.WriteForbiddenAsync(context.HttpContext, problemDetails);
    }

    private static Task<bool> IsEnabledAsync(HttpContext context) =>
        context.RequestServices.GetRequiredService<IVariantFeatureManagerSnapshot>().IsEnabledAsync(BearerTokenFeature.Name, context.RequestAborted).AsTask();

    private bool IsAccessTokenOfTheTenant(ClaimsPrincipal token) =>
        string.Equals(token.FindFirstValue(BearerTokenClaims.VersionClaim), BearerTokenClaims.Version, StringComparison.Ordinal)
        && string.Equals(token.FindFirstValue(ClaimConstants.Tid), signIn.Value.TenantId, StringComparison.OrdinalIgnoreCase)
        && Guid.TryParse(token.FindFirstValue(ClaimConstants.Oid), out _)
        && BearerTokenClaims.HolderOf(token) is not null;
}
