using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.SecurityEvents;
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
// application, which leaves out the id tokens the sign-in receives for the same audience, and a person's token is refused
// while the person's record does not admit them (IPersonAdmission), as their session would be. A refused token is answered
// invalid_token without the reason (RFC 6750, section 3.1), and every refusal carries a problem like the session cookie's
// and is recorded as a security event with its reason and the client application the token names. The handler also reports
// a request its caller abandoned while the token was read, which refused no token, so that one is not recorded.
internal sealed class BearerTokenEvents(IOptions<EntraSignInOptions> signIn, IProblemDetailsService problemDetails) : JwtBearerEvents
{
    public const string NoTokenChallenge = JwtBearerDefaults.AuthenticationScheme;

    public const string InvalidTokenChallenge = $"{NoTokenChallenge} error=\"invalid_token\"";

    public const string InsufficientScopeChallenge = $"{NoTokenChallenge} error=\"insufficient_scope\"";

    public const string NotAnAccessTokenMessage = "The token is not a v2.0 access token issued in this tenant to a person or an application.";

    public const string NotAnAccessTokenRefusal = "not-an-access-token";

    public const string NotAdmittedMessage = "The person the token was issued to is not admitted.";

    public const string NotAdmittedRefusal = "not-admitted";

    public const string InsufficientScopeRefusal = "insufficient-scope";

    public override async Task MessageReceived(MessageReceivedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!await IsEnabledAsync(context.HttpContext).ConfigureAwait(false))
        {
            context.NoResult();
        }
    }

    public override Task AuthenticationFailed(AuthenticationFailedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Exception is OperationCanceledException)
        {
            return Task.CompletedTask;
        }

        return SignInAudit.TryRecordAsync(
            context.HttpContext,
            SecurityEventKind.BearerTokenRefused,
            BearerTokenRefusals.Describe(context.Exception),
            clientApplication: BearerTokenRefusals.ClientApplicationClaimedBy(context.Request));
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Principal is not { } token || !IsAccessTokenOfTheTenant(token))
        {
            context.Fail(NotAnAccessTokenMessage);
            await SignInAudit.TryRecordAsync(
                context.HttpContext,
                SecurityEventKind.BearerTokenRefused,
                NotAnAccessTokenRefusal,
                SignInAudit.ObjectIdOf(context.Principal),
                BearerTokenRefusals.ClientApplicationOf(context.Principal)).ConfigureAwait(false);

            return;
        }

        if (BearerTokenClaims.HolderOf(token) is BearerTokenHolder.Person
            && SignInAudit.ObjectIdOf(token) is { } person
            && !await context.HttpContext.RequestServices.GetRequiredService<IPersonAdmission>().IsAdmittedAsync(person, context.HttpContext.RequestAborted).ConfigureAwait(false))
        {
            context.Fail(NotAdmittedMessage);
            await SignInAudit.TryRecordAsync(
                context.HttpContext,
                SecurityEventKind.BearerTokenRefused,
                NotAdmittedRefusal,
                person,
                BearerTokenRefusals.ClientApplicationOf(token)).ConfigureAwait(false);
        }
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

    public override async Task Forbidden(ForbiddenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.Headers.Append(HeaderNames.WWWAuthenticate, InsufficientScopeChallenge);
        var holder = context.HttpContext.User;
        await SignInAudit.TryRecordAsync(
            context.HttpContext,
            SecurityEventKind.BearerTokenRefused,
            InsufficientScopeRefusal,
            SignInAudit.ObjectIdOf(holder),
            BearerTokenRefusals.ClientApplicationOf(holder)).ConfigureAwait(false);

        await AuthenticationProblems.WriteForbiddenAsync(context.HttpContext, problemDetails).ConfigureAwait(false);
    }

    private static Task<bool> IsEnabledAsync(HttpContext context) =>
        context.RequestServices.GetRequiredService<IVariantFeatureManagerSnapshot>().IsEnabledAsync(BearerTokenFeature.Name, context.RequestAborted).AsTask();

    private bool IsAccessTokenOfTheTenant(ClaimsPrincipal token) =>
        string.Equals(token.FindFirstValue(BearerTokenClaims.VersionClaim), BearerTokenClaims.Version, StringComparison.Ordinal)
        && string.Equals(token.FindFirstValue(ClaimConstants.Tid), signIn.Value.TenantId, StringComparison.OrdinalIgnoreCase)
        && Guid.TryParse(token.FindFirstValue(ClaimConstants.Oid), out _)
        && BearerTokenClaims.HolderOf(token) is not null;
}
