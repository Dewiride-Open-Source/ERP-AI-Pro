using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.Testing.Blob;
using Dewiride.Erp.Testing.Sql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

    private const string FeatureFlagsSection = "feature_management:feature_flags:";

    private readonly Dictionary<string, string> _configuration = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<Action<IEndpointRouteBuilder>> _testEndpoints = [];

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
            builder.UseSetting(AttachmentsEncryptionKeyKey, $"test:{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}");
        }

        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
        builder.UseSetting("APPCONFIG_ENDPOINT", string.Empty);
        builder.UseSetting(ConfigurationSourceSetting, InMemorySource);
        builder.ConfigureServices(services =>
        {
            services.AddTransient<IStartupFilter, ThrowingRouteStartupFilter>();
            services.AddTransient<IStartupFilter>(_ => new TestEndpointsStartupFilter(_testEndpoints));
        });
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
