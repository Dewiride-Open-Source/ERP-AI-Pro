using System.Diagnostics;
using System.Reflection;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Configuration;

public static class ErpConfigurationExtensions
{
    private const string SecretsDirectory = "/run/secrets";

    public static IHostApplicationBuilder AddErpConfiguration(this IHostApplicationBuilder builder, Assembly hostAssembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(hostAssembly);

        if (Directory.Exists(SecretsDirectory))
        {
            builder.Configuration.AddKeyPerFile(SecretsDirectory, optional: true, reloadOnChange: false);
        }

        builder.Services
            .AddOptions<ErpHostOptions>()
            .BindConfiguration(ErpHostOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var version = ResolveVersion(hostAssembly);
        var startedAt = new DateTimeOffset(Process.GetCurrentProcess().StartTime.ToUniversalTime(), TimeSpan.Zero);

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(provider =>
            new ApplicationInfo(provider.GetRequiredService<IOptions<ErpHostOptions>>().Value.ApplicationName, version, startedAt));

        return builder;
    }

    private static string ResolveVersion(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informational) ? assembly.GetName().Version?.ToString() ?? "0.0.0" : informational;
    }
}
