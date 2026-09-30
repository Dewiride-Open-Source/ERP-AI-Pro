using Dewiride.Erp.BuildingBlocks.Authentication.Endpoints;
using Dewiride.Erp.BuildingBlocks.Authentication.OpenIdConnect;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.TokenCacheProviders.InMemory;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Dewiride.Erp.BuildingBlocks.Authentication;

public static class AuthenticationRegistration
{
    private const string IdentityWebLogCategory = "Microsoft.Identity.Web";

    // Microsoft.Identity.Web hands the plain OpenID Connect handler only a client secret, and the app registration has a
    // certificate only, so the code is redeemed through its token acquisition, which signs the client assertion with it.
    // No scope is requested beyond sign-in: the tokens are cached only for as long as a session can live.
    public static IHostApplicationBuilder AddErpAuthentication(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<EntraSignInOptions>()
            .BindConfiguration(EntraSignInOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EntraSignInOptions>, EntraSignInOptionsValidator>());

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddMicrosoftIdentityWebApp(static _ => { }, cookieScheme: CookieAuthenticationDefaults.AuthenticationScheme)
            .EnableTokenAcquisitionToCallDownstreamApi()
            .AddInMemoryTokenCaches();

        builder.Services.TryAddSingleton<SignInEvents>();
        builder.Services.TryAddScoped<SessionCookieEvents>();
        builder.Services.AddSingleton<IConfigureOptions<MicrosoftIdentityOptions>, MicrosoftIdentityOptionsSetup>();
        builder.Services.AddSingleton<IConfigureOptions<ConfidentialClientApplicationOptions>, ConfidentialClientOptionsSetup>();
        builder.Services.AddSingleton<IConfigureOptions<CookieAuthenticationOptions>, SessionCookieOptionsSetup>();
        builder.Services.AddOptions<MsalMemoryTokenCacheOptions>().Configure<IOptions<EntraSignInOptions>>((cache, signIn) =>
            cache.AbsoluteExpirationRelativeToNow = signIn.Value.SessionLifetime);

        // No scheme is named, so a caller signed in through any registered scheme satisfies it.
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddValidation();

        // Both libraries write the error description Entra returns, which can quote the person's account, into their own
        // records, and SignInEvents already records every failed sign-in by category and OAuth error code. The configured
        // log level rules are added before these, so these win over any rule for the same or a shorter category. A longer
        // category would override them, and so would any rule configured for one logging provider, for that provider.
        builder.Logging.AddFilter(IdentityWebLogCategory, LogLevel.Warning);
        builder.Logging.AddFilter(typeof(OpenIdConnectHandler).FullName, LogLevel.Critical);

        return builder;
    }

    public static IEndpointRouteBuilder MapErpAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        AuthEndpoints.Map(endpoints);

        return endpoints;
    }
}
