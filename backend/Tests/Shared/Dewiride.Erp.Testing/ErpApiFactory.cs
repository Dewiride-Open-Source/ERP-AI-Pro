using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;
using Dewiride.Erp.BuildingBlocks.Authentication.Graph;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authentication.BearerTokens;
using Dewiride.Erp.Testing.Blob;
using Dewiride.Erp.Testing.Deployment;
using Dewiride.Erp.Testing.Graph;
using Dewiride.Erp.Testing.Sql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Client;

namespace Dewiride.Erp.Testing;

public sealed class ErpApiFactory : WebApplicationFactory<Program>
{
    public const string ThrowingPath = "/__test/throw";

    public const string ConfigurationSourceSetting = "ERP_CONFIGURATION_SOURCE";

    public const string InMemorySource = "InMemory";

    public const string DatabaseConnectionKey = $"{DatabaseOptions.SectionName}:ConnectionString";

    public const string AttachmentsBlobServiceUriKey = $"{AttachmentsOptions.SectionName}:BlobServiceUri";

    public const string AttachmentsEmulatorHostKey = $"{AttachmentsOptions.SectionName}:EmulatorHost";

    public const string AttachmentsContainerNameKey = $"{AttachmentsOptions.SectionName}:ContainerName";

    public const string AttachmentsEncryptionKeyKey = $"{AttachmentsOptions.SectionName}:EncryptionKey";

    public const string IdentityTenantIdKey = $"{EntraSignInOptions.SectionName}:TenantId";

    public const string IdentityClientIdKey = $"{EntraSignInOptions.SectionName}:ClientId";

    public const string IdentityWebOriginKey = $"{EntraSignInOptions.SectionName}:WebOrigin";

    public const string IdentityClientCertificateKey = $"{EntraSignInOptions.SectionName}:ClientCertificate";

    public const string IdentitySessionIdleTimeoutKey = $"{EntraSignInOptions.SectionName}:SessionIdleTimeout";

    public const string IdentitySessionLifetimeKey = $"{EntraSignInOptions.SectionName}:SessionLifetime";

    public const string DataProtectionKeyIdentifierKey = KeyRingOptions.KeyIdentifierKey;

    public const string SignInPermitLimitKey = $"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.SignInPermitLimit)}";

    public const string TestSignInPermitLimit = "600";

    private const string FeatureFlagsSection = "feature_management:feature_flags:";

    // One key per test process: every factory shares the process's test database and blob container, so content one host
    // stored must stay readable, and deduplicable, for every other.
    private static readonly string TestEncryptionKey = $"test:{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}";

    private readonly Dictionary<string, string> _configuration = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<Action<IEndpointRouteBuilder>> _testEndpoints = [];

    private TestDeployment? _deployment;

    private bool _ownsDeployment;

    private bool _hostCreated;

    public ErpApiFactory()
        : this(Environments.Development)
    {
    }

    private ErpApiFactory(string environment)
    {
        Environment = environment;
    }

    public string Environment { get; }

    // Every factory is a deployment of its own unless a test gives it one to share. Production refuses a key ring kept in
    // memory, so a Production factory gets a persisted one.
    public TestDeployment Deployment
    {
        get
        {
            if (_deployment is null)
            {
                _deployment = string.Equals(Environment, Environments.Production, StringComparison.OrdinalIgnoreCase)
                    ? TestDeployment.WithPersistedKeyRing()
                    : TestDeployment.WithInMemoryKeyRing();
                _ownsDeployment = true;
            }

            return _deployment;
        }
    }

    public static ErpApiFactory ForEnvironment(string environment) => new(environment);

    public ErpApiFactory WithConfiguration(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        if (_hostCreated)
        {
            throw new InvalidOperationException("WithConfiguration must be called before the first client or service is requested from the factory.");
        }

        _configuration[key] = value;

        return this;
    }

    public ErpApiFactory WithFeature(string name, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var index = _configuration.Keys.Count(key => key.StartsWith(FeatureFlagsSection, StringComparison.OrdinalIgnoreCase) && key.EndsWith(":id", StringComparison.OrdinalIgnoreCase));

        return WithConfiguration($"{FeatureFlagsSection}{index}:id", name)
            .WithConfiguration($"{FeatureFlagsSection}{index}:enabled", enabled ? "true" : "false");
    }

    // TestServer has no client address and ignores Kestrel limits, so transport tests run on a real listener; port 0 lets
    // parallel test classes bind distinct ports. Never combine it with WithWebHostBuilder, which does not carry Kestrel over.
    public ErpApiFactory WithKestrel()
    {
        if (_hostCreated)
        {
            throw new InvalidOperationException("WithKestrel must be called before the first client or service is requested from the factory.");
        }

        UseKestrel(0);

        return this;
    }

    public ErpApiFactory WithTestEndpoints(Action<IEndpointRouteBuilder> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (_hostCreated)
        {
            throw new InvalidOperationException("WithTestEndpoints must be called before the first client or service is requested from the factory.");
        }

        _testEndpoints.Add(map);

        return this;
    }

    // The test owns the deployment, so two factories can be one deployment: the hosts of a restart, or two instances side by side.
    public ErpApiFactory WithDeployment(TestDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);
        if (_hostCreated)
        {
            throw new InvalidOperationException("WithDeployment must be called before the first client or service is requested from the factory.");
        }

        if (_ownsDeployment)
        {
            _deployment?.Dispose();
        }

        _deployment = deployment;
        _ownsDeployment = false;

        return this;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_ownsDeployment)
        {
            _deployment?.Dispose();
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        _hostCreated = true;

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environment);
        foreach (var (key, value) in _configuration)
        {
            builder.UseSetting(key, value);
        }

        if (!_configuration.ContainsKey(DatabaseConnectionKey))
        {
            builder.UseSetting(DatabaseConnectionKey, SqlTestDatabase.Current.ConnectionString);
        }

        if (!_configuration.ContainsKey(AttachmentsEmulatorHostKey) && !_configuration.ContainsKey(AttachmentsBlobServiceUriKey))
        {
            builder.UseSetting(AttachmentsBlobServiceUriKey, string.Empty);
            builder.UseSetting(AttachmentsEmulatorHostKey, BlobTestContainer.Current.EmulatorHost);
            if (!_configuration.ContainsKey(AttachmentsContainerNameKey))
            {
                builder.UseSetting(AttachmentsContainerNameKey, BlobTestContainer.Current.ContainerName);
            }
        }

        if (!_configuration.ContainsKey(AttachmentsEncryptionKeyKey))
        {
            builder.UseSetting(AttachmentsEncryptionKeyKey, TestEncryptionKey);
        }

        UseSettingUnlessSupplied(builder, IdentityTenantIdKey, TestIdentityProvider.TenantId);
        UseSettingUnlessSupplied(builder, IdentityClientIdKey, TestIdentityProvider.ClientId);
        UseSettingUnlessSupplied(builder, IdentityWebOriginKey, TestIdentityProvider.WebOrigin);
        UseSettingUnlessSupplied(builder, IdentityClientCertificateKey, TestSignInCertificate.Base64);
        UseSettingUnlessSupplied(builder, SignInPermitLimitKey, TestSignInPermitLimit);

        var deployment = Deployment;
        if (deployment.KeyIdentifier is { } keyIdentifier)
        {
            UseSettingUnlessSupplied(builder, DataProtectionKeyIdentifierKey, keyIdentifier.AbsoluteUri);
        }

        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
        builder.UseSetting("APPCONFIG_ENDPOINT", string.Empty);
        builder.UseSetting(ConfigurationSourceSetting, InMemorySource);
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
            services.PostConfigure<AuthenticationOptions>(options => options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName);
            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, TestIdentityProvider.Configure);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, TestTokenIssuer.Configure);
            services.AddSingleton<TestTokenEndpoint>();
            services.AddSingleton<IMsalHttpClientFactory>(provider => provider.GetRequiredService<TestTokenEndpoint>());
            services.AddSingleton<TestDirectory>();
            services.AddHttpClient(MicrosoftGraph.HttpClientName).ConfigurePrimaryHttpMessageHandler(provider => provider.GetRequiredService<TestDirectory>().CreateHandler());
            services.AddTransient<IStartupFilter, ThrowingRouteStartupFilter>();
            services.AddTransient<IStartupFilter>(_ => new TestEndpointsStartupFilter(_testEndpoints));
            deployment.Register(services);
        });
    }

    private void UseSettingUnlessSupplied(IWebHostBuilder builder, string key, string value)
    {
        if (!_configuration.ContainsKey(key))
        {
            builder.UseSetting(key, value);
        }
    }

    private sealed class TestEndpointsStartupFilter(IReadOnlyList<Action<IEndpointRouteBuilder>> maps) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            if (maps.Count > 0)
            {
                app.UseEndpoints(routes =>
                {
                    foreach (var map in maps)
                    {
                        map(routes);
                    }
                });
            }
        };
    }

    private sealed class ThrowingRouteStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Use((context, pipeline) =>
                context.Request.Path.StartsWithSegments(ThrowingPath)
                    ? throw new InvalidOperationException("Deliberate failure for pipeline tests.")
                    : pipeline(context));
        };
    }
}
