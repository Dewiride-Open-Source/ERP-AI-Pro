using Dewiride.Erp.BuildingBlocks.Persistence.Migrations;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;
using Dewiride.Erp.Host.Composition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Host.Migrator;

// The host is built but never started: starting it would run the modules' hosted services, which belong to the API.
internal static partial class MigratorApplication
{
    public const string MigrateCommand = "migrate";

    public const string StatusCommand = "status";

    public const string Usage = "Usage: Dewiride.Erp.Host.Migrator migrate|status [--Key=Value configuration arguments]";

    public static async Task<int> RunAsync(IReadOnlyList<string> args, Action<HostApplicationBuilder>? configure, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);

        var command = args.Count > 0 ? args[0] : null;
        if (command is not (MigrateCommand or StatusCommand))
        {
            await Console.Error.WriteLineAsync(Usage).ConfigureAwait(false);
            return MigratorExitCodes.Usage;
        }

        IHost? built = null;
        try
        {
            built = BuildHost(args, configure);
            built.Services.GetRequiredService<IStartupValidator>().Validate();
        }
        catch (Exception exception)
        {
            // Nothing has touched the database yet, so whatever failed while composing, loading the configuration store and its
            // Key Vault references or validating options is a configuration problem; no logger exists before the host does.
            built?.Dispose();
            await Console.Error.WriteLineAsync($"The migrator configuration is invalid: {exception}").ConfigureAwait(false);
            return MigratorExitCodes.InvalidConfiguration;
        }

        using var host = built;
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MigratorApplication).FullName!);

        try
        {
            return command == MigrateCommand
                ? await MigrateAsync(host.Services, logger, cancellationToken).ConfigureAwait(false)
                : await ReportStatusAsync(host.Services, logger, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogCancelled(logger);
            return MigratorExitCodes.Failed;
        }
        catch (Exception exception)
        {
            LogFailed(logger, exception);
            return MigratorExitCodes.Failed;
        }
    }

    private static IHost BuildHost(IReadOnlyList<string> args, Action<HostApplicationBuilder>? configure)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [.. args.Skip(1)], ContentRootPath = AppContext.BaseDirectory });
        configure?.Invoke(builder);
        builder.AddErpPlatform(typeof(MigratorApplication).Assembly);
        builder.Services.AddOptions<MigratorDatabaseOptions>().BindConfiguration(MigratorDatabaseOptions.SectionName);
        builder.Services.AddSingleton<IPostConfigureOptions<DatabaseOptions>, MigratorConnectionStringSetup>();

        return builder.Build();
    }

    private static async Task<int> MigrateAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
    {
        await services.GetRequiredService<DatabaseMigrator>().MigrateAllAsync(cancellationToken).ConfigureAwait(false);
        var seeding = services.GetRequiredService<SeedRunner>();
        await seeding.RunAsync(cancellationToken).ConfigureAwait(false);
        LogMigrated(logger, seeding.Seeders.Count);

        return MigratorExitCodes.Succeeded;
    }

    private static async Task<int> ReportStatusAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
    {
        var pending = await services.GetRequiredService<DatabaseMigrator>().GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var schema in pending)
        {
            LogSchemaStatus(logger, schema.Schema, schema.Migrations.Count, schema.Migrations);
        }

        return pending.Any(schema => schema.Migrations.Count > 0) ? MigratorExitCodes.MigrationsPending : MigratorExitCodes.Succeeded;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Every schema is migrated and {SeederCount} seeder(s) ran.")]
    private static partial void LogMigrated(ILogger logger, int seederCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Schema {Schema} has {PendingCount} pending migration(s): {Migrations}")]
    private static partial void LogSchemaStatus(ILogger logger, string schema, int pendingCount, IReadOnlyList<string> migrations);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The migrator was cancelled before it finished.")]
    private static partial void LogCancelled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "The migrator failed.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
