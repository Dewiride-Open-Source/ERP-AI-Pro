using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Testing;

public sealed class ErpApiFactory : WebApplicationFactory<Program>
{
    public const string ThrowingPath = "/__test/throw";

    public const string ConfigurationSourceSetting = "ERP_CONFIGURATION_SOURCE";

    public const string InMemorySource = "InMemory";

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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environment);
        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
        builder.UseSetting("APPCONFIG_ENDPOINT", string.Empty);
        builder.UseSetting(ConfigurationSourceSetting, InMemorySource);
        builder.ConfigureServices(services => services.AddTransient<IStartupFilter, ThrowingRouteStartupFilter>());
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
