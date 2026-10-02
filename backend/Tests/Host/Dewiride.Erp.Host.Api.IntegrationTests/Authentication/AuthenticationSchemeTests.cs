using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;
using Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.TokenCacheProviders;
using Microsoft.Identity.Web.TokenCacheProviders.Distributed;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

public sealed class AuthenticationSchemeTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string Cookies = CookieAuthenticationDefaults.AuthenticationScheme;

    private const string OpenIdConnect = OpenIdConnectDefaults.AuthenticationScheme;

    [Fact]
    public async Task Schemes_Registered_AreTheCookieAndOpenIdConnectSchemesBesideTheTestScheme()
    {
        var schemes = await factory.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();

        Assert.Equal([Cookies, OpenIdConnect, TestAuthHandler.SchemeName], schemes.Select(scheme => scheme.Name).Order(StringComparer.Ordinal));
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
        Assert.Equal(AuthPaths.LoginPage, options.SignedOutRedirectUri);
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
}
