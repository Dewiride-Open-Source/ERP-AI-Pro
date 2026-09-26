using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using Dewiride.Erp.BuildingBlocks.Configuration.AppConfiguration;
using Dewiride.Erp.BuildingBlocks.Configuration.Credentials;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Configuration;

public static class ErpConfigurationExtensions
{
    public static IHostApplicationBuilder AddErpConfiguration(this IHostApplicationBuilder builder, Assembly hostAssembly) =>
        builder.AddErpConfiguration(hostAssembly, Environment.GetEnvironmentVariable, configureProvider: null);

    internal static IHostApplicationBuilder AddErpConfiguration(
        this IHostApplicationBuilder builder,
        Assembly hostAssembly,
        Func<string, string?> environmentVariable,
        Action<AzureAppConfigurationOptions>? configureProvider)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(hostAssembly);

        if (Directory.Exists(SecretsDirectorySource.Directory))
        {
            SecretsDirectorySource.Add(builder.Configuration, SecretsDirectorySource.Directory);
        }

        var info = ErpConfigurationSourceResolver.Resolve(builder.Configuration, builder.Environment);
        var refresh = ReadRefreshOptions(builder.Configuration);

        if (info.Source == ErpConfigurationSource.AppConfiguration)
        {
            var credential = AzureCredentialFactory.Create(builder.Environment, environmentVariable);
            AppConfigurationSetup.RequireMicrosoftFeatureFlagSchema();
            builder.Configuration.AddAzureAppConfiguration(
                options =>
                {
                    AppConfigurationSetup.Configure(options, info.Endpoint!, info.Label!, credential, refresh);
                    configureProvider?.Invoke(options);
                },
                optional: false);
            builder.Services.AddAzureAppConfiguration();
            builder.Services.AddSingleton(credential);
        }
        else
        {
            // Resolved only by a client that reaches Azure, so hosts that never do (tests, the emulator mode) need no identity.
            builder.Services.TryAddSingleton(provider => AzureCredentialFactory.Create(provider.GetRequiredService<IHostEnvironment>(), environmentVariable));
        }

        builder.Services
            .AddOptions<ErpHostOptions>()
            .BindConfiguration(ErpHostOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ErpHostOptions>, ErpHostOptionsValidator>());

        builder.Services.AddSingleton(refresh);
        builder.Services.AddSingleton(info);
        builder.Properties[typeof(ErpConfigurationInfo)] = info;

        var version = ResolveVersion(hostAssembly);
        var startedAt = new DateTimeOffset(Process.GetCurrentProcess().StartTime.ToUniversalTime(), TimeSpan.Zero);

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(provider =>
            new ApplicationInfo(provider.GetRequiredService<IOptions<ErpHostOptions>>().Value.ApplicationName, version, startedAt));

        return builder;
    }

    public static ErpConfigurationInfo GetErpConfigurationInfo(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.Properties.TryGetValue(typeof(ErpConfigurationInfo), out var value) && value is ErpConfigurationInfo info
            ? info
            : throw new InvalidOperationException("Call AddErpConfiguration before GetErpConfigurationInfo.");
    }

    public static IApplicationBuilder UseErpConfigurationRefresh(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.ApplicationServices.GetRequiredService<ErpConfigurationInfo>().Source == ErpConfigurationSource.AppConfiguration)
        {
            app.UseMiddleware<ConfigurationRefreshMiddleware>();
        }

        return app;
    }

    private static AppConfigurationRefreshOptions ReadRefreshOptions(IConfiguration bootstrap)
    {
        var refresh = bootstrap.GetSection(AppConfigurationRefreshOptions.SectionName).Get<AppConfigurationRefreshOptions>() ?? new();
        try
        {
            Validator.ValidateObject(refresh, new ValidationContext(refresh), validateAllProperties: true);
        }
        catch (ValidationException exception)
        {
            throw new InvalidOperationException(
                $"{AppConfigurationRefreshOptions.SectionName} is invalid: {exception.Message} Set it in appsettings.json, an environment variable or user secrets.",
                exception);
        }

        return refresh;
    }

    private static string ResolveVersion(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informational) ? assembly.GetName().Version?.ToString() ?? "0.0.0" : informational;
    }
}
