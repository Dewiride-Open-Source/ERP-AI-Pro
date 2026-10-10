using Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;
using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;
using Dewiride.Erp.BuildingBlocks.Authentication.Endpoints;
using Dewiride.Erp.BuildingBlocks.Authentication.Logging;
using Dewiride.Erp.BuildingBlocks.Authentication.OpenIdConnect;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.SecurityEvents;
using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;
using Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;
using Dewiride.Erp.BuildingBlocks.Modules.Features;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Dewiride.Erp.BuildingBlocks.Authentication;

public static class AuthenticationRegistration
{
    private const string IdentityWebLogCategory = "Microsoft.Identity.Web";

    private const string TokenCacheLogCategory = "Microsoft.Identity.Web.TokenCacheProviders";

    // Microsoft.Identity.Web hands the plain OpenID Connect handler only a client secret, and the app registration has a
    // certificate only, so the code is redeemed through its token acquisition, which signs the client assertion with it.
    // No scope is requested beyond sign-in: the tokens are cached only for as long as a session can live. The bearer scheme is
    // the framework's own handler rather than Microsoft.Identity.Web's web API, whose defaults also accept a v1.0 token of the
    // tenant and add scope requirements to the default policy; it signs in only the routes that call RequireBearerToken.
    public static IHostApplicationBuilder AddErpAuthentication(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<EntraSignInOptions>()
            .BindConfiguration(EntraSignInOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EntraSignInOptions>, EntraSignInOptionsValidator>());

        var authentication = builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = RouteSignInScheme.Name;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddPolicyScheme(RouteSignInScheme.Name, displayName: null, static options => options.ForwardDefaultSelector = RouteSignInScheme.Select);
        authentication
            .AddMicrosoftIdentityWebApp(static _ => { }, cookieScheme: CookieAuthenticationDefaults.AuthenticationScheme)
            .EnableTokenAcquisitionToCallDownstreamApi();
        authentication.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, static _ => { });
        builder.Services.AddErpTokenCache();
        builder.AddErpDataProtection();

        // With Cookie.SecurePolicy Always, DefaultAntiforgery refuses every call, validation included, on a request it does not
        // see as HTTPS, and the web server calls the API over plain HTTP inside the host; the framework therefore issues no
        // cookie, and AntiforgeryCookies writes both, Secure.
        builder.Services.AddAntiforgery(static options =>
        {
            options.HeaderName = AntiforgeryTokens.HeaderName;
            options.FormFieldName = AntiforgeryTokens.FormFieldName;
            options.Cookie.Name = AntiforgeryTokens.CookieName;
        });
        builder.Services.TryAddSingleton<AntiforgeryCookies>();

        builder.Services.AddErpPlatformFeature(BearerTokenFeature.Name, enabledByDefault: false);
        builder.Services.TryAddSingleton<BearerTokenEvents>();
        builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, BearerTokenOptionsSetup>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, BearerTokenAccessHandler>());

        builder.Services.TryAddSingleton<BoundedIdentityModelLogger>();
        builder.Services.TryAddSingleton<AnonymousSecurityEventBudget>();
        builder.Services.AddHostedService<IdentityModelLoggerInstaller>();
        builder.Services.TryAddSingleton<SignInEvents>();
        builder.Services.TryAddSingleton<SessionRevocations>();
        builder.Services.TryAddSingleton<EntraSessions>();
        builder.Services.TryAddScoped<FrontChannelSignOut>();
        builder.Services.TryAddScoped<SessionCookieEvents>();
        builder.Services.AddSingleton<IConfigureOptions<MicrosoftIdentityOptions>, MicrosoftIdentityOptionsSetup>();
        builder.Services.AddSingleton<IConfigureOptions<ConfidentialClientApplicationOptions>, ConfidentialClientOptionsSetup>();
        builder.Services.AddSingleton<IConfigureOptions<CookieAuthenticationOptions>, SessionCookieOptionsSetup>();

        // No scheme is named, so the caller the default scheme signed in satisfies it: the session cookie or the bearer token the
        // route takes (RouteSignInScheme).
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddValidation();

        // Both libraries write the error description Entra returns, which can quote the person's account, into their own
        // records, and SignInEvents already records every failed sign-in by category and OAuth error code; the token cache
        // adapter writes the account id into its debug records and into a warning. The configured log level rules are added
        // before these, so these win over any rule for the same or a shorter category. A longer category would override
        // them, and so would any rule configured for one logging provider, for that provider.
        builder.Logging.AddFilter(IdentityWebLogCategory, LogLevel.Warning);
        builder.Logging.AddFilter(typeof(OpenIdConnectHandler).FullName, LogLevel.Critical);
        builder.Logging.AddFilter(TokenCacheLogCategory, LogLevel.Error);

        return builder;
    }

    public static IEndpointRouteBuilder MapErpAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        AuthEndpoints.Map(endpoints);

        return endpoints;
    }

    public static IApplicationBuilder UseErpAntiforgery(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<AntiforgeryValidationMiddleware>();

        return app.UseAntiforgery();
    }
}
