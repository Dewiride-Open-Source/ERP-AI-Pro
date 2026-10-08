using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;
using Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.TokenCacheProviders;
using Microsoft.Identity.Web.TokenCacheProviders.Distributed;
using Microsoft.IdentityModel.Tokens;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

public sealed class AuthenticationSchemeTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string Cookies = CookieAuthenticationDefaults.AuthenticationScheme;

    private const string OpenIdConnect = OpenIdConnectDefaults.AuthenticationScheme;

    private const string Bearer = JwtBearerDefaults.AuthenticationScheme;

    [Fact]
    public async Task Schemes_Registered_AreTheCookieOpenIdConnectBearerAndRouteSchemesBesideTheTestScheme()
    {
        var schemes = await factory.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();

        Assert.Equal([Bearer, Cookies, RouteSignInScheme.Name, OpenIdConnect, TestAuthHandler.SchemeName], schemes.Select(scheme => scheme.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void RouteSignInScheme_ForARouteThatTakesBearerTokens_ForwardsToTheBearerSchemeAndOtherwiseToTheCookie()
    {
        var options = factory.Services.GetRequiredService<IOptionsMonitor<PolicySchemeOptions>>().Get(RouteSignInScheme.Name);
        var bearerRoute = new Endpoint(null, new EndpointMetadataCollection(new BearerTokenRouteMetadata(new BearerTokenAccess(["Erp.Test.Read"], []))), "bearer");
        var sessionRoute = new Endpoint(null, EndpointMetadataCollection.Empty, "session");

        Assert.NotNull(options.ForwardDefaultSelector);
        Assert.Equal(Bearer, options.ForwardDefaultSelector(ContextFor(bearerRoute)));
        Assert.Equal(Cookies, options.ForwardDefaultSelector(ContextFor(sessionRoute)));
        Assert.Equal(Cookies, options.ForwardDefaultSelector(ContextFor(endpoint: null)));
    }

    [Fact]
    public void JwtBearerOptions_Configured_ValidateV2AccessTokensOfTheTenantForTheRegistrationWithRawClaimNames()
    {
        var options = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(Bearer);
        var issuer = $"{TestIdentityProvider.Instance}{TestIdentityProvider.TenantId}/v2.0";

        Assert.Equal(issuer, options.Authority);
        Assert.Equal(TestIdentityProvider.ClientId, options.TokenValidationParameters.ValidAudience);
        Assert.Null(options.TokenValidationParameters.ValidAudiences);
        Assert.Null(options.TokenValidationParameters.AudienceValidator);
        Assert.Equal(issuer, options.TokenValidationParameters.ValidIssuer);
        Assert.Null(options.TokenValidationParameters.IssuerValidator);
        Assert.Equal([SecurityAlgorithms.RsaSha256], options.TokenValidationParameters.ValidAlgorithms);
        Assert.False(options.MapInboundClaims);
        Assert.False(options.IncludeErrorDetails);
        Assert.False(options.SaveToken);
        Assert.Equal("preferred_username", options.TokenValidationParameters.NameClaimType);
        Assert.Equal("roles", options.TokenValidationParameters.RoleClaimType);
        Assert.Equal(typeof(BearerTokenEvents), options.EventsType);
    }

    [Fact]
    public async Task JwtBearerOptions_WithAnInstanceWithoutATrailingSlash_UseTheTenantsV2Authority()
    {
        await using var configured = new ErpApiFactory().WithConfiguration($"{EntraSignInOptions.SectionName}:Instance", "https://login.microsoftonline.com");

        var options = configured.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(Bearer);

        Assert.Equal($"https://login.microsoftonline.com/{TestIdentityProvider.TenantId}/v2.0", options.Authority);
        Assert.Equal(options.Authority, options.TokenValidationParameters.ValidIssuer);
    }

    [Fact]
    public void Endpoints_NameNoSchemeButTheOneTheirRouteSignsInWith()
    {
        var offenders = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Select(endpoint => (Endpoint: endpoint, Schemes: endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>().SelectMany(policy => policy.AuthenticationSchemes)
                .Concat(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().SelectMany(data => data.AuthenticationSchemes?.Split(',', StringSplitOptions.TrimEntries) ?? []))
                .Distinct(StringComparer.Ordinal)
                .ToList()))
            .Where(route => route.Endpoint.Metadata.GetMetadata<BearerTokenRouteMetadata>() is null
                ? route.Schemes.Any(scheme => scheme != Cookies)
                : !route.Schemes.SequenceEqual([Bearer]))
            .Select(route => route.Endpoint.DisplayName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void AuthenticationOptions_ConfiguredByTheApi_AuthenticateWithTheRouteSchemeAndUseTheCookieForEverythingElse()
    {
        var options = new AuthenticationOptions();
        foreach (var configure in factory.Services.GetServices<IConfigureOptions<AuthenticationOptions>>())
        {
            configure.Configure(options);
        }

        Assert.Equal(RouteSignInScheme.Name, options.DefaultAuthenticateScheme);
        Assert.Equal(Cookies, options.DefaultScheme);
        Assert.Equal(Cookies, options.DefaultChallengeScheme);
        Assert.Equal(Cookies, options.DefaultForbidScheme);
    }

    [Fact]
    public void AuthenticationOptions_Defaults_UseTheCookieSchemeForChallengeSignInSignOutAndForbid()
    {
        var options = factory.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        Assert.Equal(Cookies, options.DefaultScheme);
        Assert.Equal(Cookies, options.DefaultChallengeScheme);
        Assert.Equal(Cookies, options.DefaultSignInScheme);
        Assert.Equal(Cookies, options.DefaultSignOutScheme);
        Assert.Equal(Cookies, options.DefaultForbidScheme);
        Assert.Equal(TestAuthHandler.SchemeName, options.DefaultAuthenticateScheme);
    }

    [Fact]
    public void OpenIdConnectOptions_Configured_UseTheCertificateCodeFlowWithPkceOnTheFixedPaths()
    {
        var options = factory.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnect);

        Assert.Equal(TestIdentityProvider.Issuer, options.Authority);
        Assert.Equal(TestIdentityProvider.ClientId, options.ClientId);
        Assert.Null(options.ClientSecret);
        Assert.Equal("code", options.ResponseType);
        Assert.Equal("form_post", options.ResponseMode);
        Assert.True(options.UsePkce);
        Assert.Equal(PushedAuthorizationBehavior.Disable, options.PushedAuthorizationBehavior);
        Assert.False(options.SaveTokens);
        Assert.False(options.MapInboundClaims);
        Assert.Equal(Cookies, options.SignInScheme);
        Assert.Equal(["offline_access", "openid", "profile"], options.Scope.Order(StringComparer.Ordinal));
        Assert.Equal(AuthPaths.SignInCallback, options.CallbackPath);
        Assert.Equal(AuthPaths.SignedOutCallback, options.SignedOutCallbackPath);
        Assert.False(options.RemoteSignOutPath.HasValue);
        Assert.Equal(AuthPaths.SignedOutPage, options.SignedOutRedirectUri);
        Assert.DoesNotContain(options.ClaimActions, action => action.ClaimType == "iss");
        Assert.Contains(options.ClaimActions, action => action.ClaimType == "nonce");
        Assert.Equal("roles", options.TokenValidationParameters.RoleClaimType);
        Assert.Equal("preferred_username", options.TokenValidationParameters.NameClaimType);
        Assert.All(new[] { options.CorrelationCookie, options.NonceCookie }, cookie =>
        {
            Assert.True(cookie.HttpOnly);
            Assert.Equal(CookieSecurePolicy.Always, cookie.SecurePolicy);
            Assert.Equal(SameSiteMode.None, cookie.SameSite);
        });
    }

    [Fact]
    public void MicrosoftIdentityOptions_Configured_RedeemCodesWithTheSignInCertificateOnly()
    {
        var options = factory.Services.GetRequiredService<IOptionsMonitor<MicrosoftIdentityOptions>>().Get(OpenIdConnect);

        Assert.Equal(TestIdentityProvider.Instance, options.Instance);
        Assert.Equal(TestIdentityProvider.TenantId, options.TenantId);
        Assert.Equal(TestIdentityProvider.ClientId, options.ClientId);
        Assert.Null(options.ClientSecret);
        var credential = Assert.Single(options.ClientCredentials ?? []);
        Assert.Equal(CredentialSource.Base64Encoded, credential.SourceType);
        Assert.Equal(TestSignInCertificate.Base64, credential.Base64EncodedValue);
    }

    [Fact]
    public void CookieOptions_Configured_IssueTheHostPrefixedSessionCookieThatSlidesWithinTheIdleTimeout()
    {
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(Cookies);

        Assert.Equal("__Host-erp-session", options.Cookie.Name);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, options.Cookie.SameSite);
        Assert.Equal("/", options.Cookie.Path);
        Assert.Null(options.Cookie.Domain);
        Assert.Equal(TimeSpan.FromMinutes(30), options.ExpireTimeSpan);
        Assert.True(options.SlidingExpiration);
        Assert.Equal(typeof(SessionCookieEvents), options.EventsType);
    }

    [Fact]
    public void ConfidentialClientOptions_RedirectUri_IsTheCallbackOnTheWebOrigin()
    {
        var options = factory.Services.GetRequiredService<IOptionsMonitor<ConfidentialClientApplicationOptions>>().Get(OpenIdConnect);

        Assert.Equal($"{TestIdentityProvider.WebOrigin}{AuthPaths.SignInCallback}", options.RedirectUri);
    }

    [Fact]
    public void TokenCacheProvider_Resolved_IsTheDistributedAdapterAndTheOnlyRegistration()
    {
        Assert.IsType<MsalDistributedTokenCacheAdapter>(factory.Services.GetRequiredService<IMsalTokenCacheProvider>());
        Assert.Single(factory.Services.GetServices<IMsalTokenCacheProvider>());
    }

    [Fact]
    public void TokenCacheOptions_Configured_ExpireEntriesFiveMinutesAfterTheSessionWithoutTheMemoryLevelOrTheAdapterEncryption()
    {
        var options = factory.Services.GetRequiredService<IOptions<MsalDistributedTokenCacheAdapterOptions>>().Value;

        Assert.Equal(TimeSpan.FromHours(12) + TimeSpan.FromMinutes(5), options.AbsoluteExpirationRelativeToNow);
        Assert.Equal(TimeSpan.FromMinutes(30) + TimeSpan.FromMinutes(5), options.SlidingExpiration);
        Assert.Null(options.AbsoluteExpiration);
        Assert.True(options.DisableL1Cache);
        Assert.False(options.Encrypt);
        Assert.NotNull(options.OnL2CacheFailure);
    }

    [Fact]
    public void TokenCacheOptions_OnAStoreFailure_ThrowTheUnavailableExceptionInsteadOfRetrying()
    {
        var options = factory.Services.GetRequiredService<IOptions<MsalDistributedTokenCacheAdapterOptions>>().Value;
        var failure = new TimeoutException("The token cache store did not answer.");

        var thrown = Assert.Throws<TokenCacheUnavailableException>(() => options.OnL2CacheFailure!(failure));

        Assert.Same(failure, thrown.InnerException);
        Assert.Equal(TokenCacheUnavailableException.DefaultMessage, thrown.Message);
    }

    [Fact]
    public void TokenCacheOptions_OnACancelledStoreOperation_RethrowTheCancellationItself()
    {
        var options = factory.Services.GetRequiredService<IOptions<MsalDistributedTokenCacheAdapterOptions>>().Value;
        var cancellation = new OperationCanceledException("The request was cancelled.");

        Assert.Same(cancellation, Assert.Throws<OperationCanceledException>(() => options.OnL2CacheFailure!(cancellation)));
    }

    [Fact]
    public async Task SessionOptions_Configured_SetTheCookieWindowAndTheTokenCacheExpiration()
    {
        await using var configured = new ErpApiFactory()
            .WithConfiguration(ErpApiFactory.IdentitySessionIdleTimeoutKey, "00:10:00")
            .WithConfiguration(ErpApiFactory.IdentitySessionLifetimeKey, "02:00:00");

        var cookie = configured.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(Cookies);
        var cache = configured.Services.GetRequiredService<IOptions<MsalDistributedTokenCacheAdapterOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(10), cookie.ExpireTimeSpan);
        Assert.True(cookie.SlidingExpiration);
        Assert.Equal(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(5), cache.AbsoluteExpirationRelativeToNow);
        Assert.Equal(TimeSpan.FromMinutes(10) + TimeSpan.FromMinutes(5), cache.SlidingExpiration);
    }

    private static DefaultHttpContext ContextFor(Endpoint? endpoint)
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(endpoint);

        return context;
    }
}
