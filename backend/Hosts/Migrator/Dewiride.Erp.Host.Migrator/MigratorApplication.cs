using System.Collections.Frozen;
using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Persistence.Migrations;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.BuildingBlocks.Persistence.Retention;
using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;
using Dewiride.Erp.Host.Composition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Host.Migrator;

// The host is built but never started: starting it would run the modules' hosted services, which belong to the API. Every
// command first refuses a database that holds a migration this release does not know, because the release's seeders and sweeps
// were written for an older schema, and status refusing too tells a deployment that the database is ahead before it replaces
// anything. The retention sweeps run here alone, under the migrator's principal, because the API's database user may not delete
// what they delete. migrate only logs a sweep that failed, because a deletion that can wait never stops a deployment; retain does
// nothing else, so it fails with the sweep and whatever schedules it sees the failure. Of the store's Key Vault references the
// host opens only the two connection strings, so the migrator's principal is granted no other secret of the vault (ADR-0050).
internal static partial class MigratorApplication
{
    public static readonly FrozenSet<string> KeyVaultReferences = new[]
    {
        $"{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.ConnectionString)}",
        $"{MigratorDatabaseOptions.SectionName}:{nameof(MigratorDatabaseOptions.MigratorConnectionString)}",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public const string MigrateCommand = "migrate";

    public const string RetainCommand = "retain";

    public const string StatusCommand = "status";

    public static readonly string Usage = string.Join(
        Environment.NewLine,
        $"Usage: Dewiride.Erp.Host.Migrator {MigrateCommand}|{RetainCommand}|{StatusCommand} [--Key=Value configuration arguments]",
        "Exit codes:",
        ExitCodeLine(MigratorExitCodes.Succeeded, "succeeded"),
        ExitCodeLine(MigratorExitCodes.Failed, "failed or was cancelled"),
        ExitCodeLine(MigratorExitCodes.InvalidConfiguration, "the configuration could not be loaded or is invalid"),
        ExitCodeLine(MigratorExitCodes.MigrationsPending, $"migrations are pending ({StatusCommand})"),
        ExitCodeLine(MigratorExitCodes.DatabaseAhead, "the database holds a migration this release does not know, so nothing ran"),
        ExitCodeLine(MigratorExitCodes.Usage, $"the command is not {MigrateCommand}, {RetainCommand} or {StatusCommand}"));

    public static async Task<int> RunAsync(IReadOnlyList<string> args, Action<HostApplicationBuilder>? configure, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);

        var command = args.Count > 0 ? args[0] : null;
        if (command is not (MigrateCommand or RetainCommand or StatusCommand))
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
            var status = await host.Services.GetRequiredService<DatabaseMigrator>().GetStatusAsync(cancellationToken).ConfigureAwait(false);
            var ahead = status.Where(schema => schema.Unknown.Count > 0).ToList();
            if (ahead.Count > 0)
            {
                ReportDatabaseAhead(ahead, command, logger);
                return MigratorExitCodes.DatabaseAhead;
            }

            return command switch
            {
                MigrateCommand => await MigrateAsync(host.Services, logger, cancellationToken).ConfigureAwait(false),
                RetainCommand => await RetainAsync(host.Services, logger, cancellationToken).ConfigureAwait(false),
                _ => ReportStatus(status, logger),
            };
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
        builder.AddErpPlatform(typeof(MigratorApplication).Assembly, KeyVaultReferences);
        builder.Services.TryAddSingleton<RetentionRunner>();
        builder.Services.AddOptions<MigratorDatabaseOptions>().BindConfiguration(MigratorDatabaseOptions.SectionName);
        builder.Services.AddSingleton<IPostConfigureOptions<DatabaseOptions>, MigratorConnectionStringSetup>();

        return builder.Build();
    }

    private static async Task<int> MigrateAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
    {
        await services.GetRequiredService<DatabaseMigrator>().MigrateAllAsync(cancellationToken).ConfigureAwait(false);
        var seeding = services.GetRequiredService<SeedRunner>();
        await seeding.RunAsync(cancellationToken).ConfigureAwait(false);
        var retention = await services.GetRequiredService<RetentionRunner>().RunAsync(cancellationToken).ConfigureAwait(false);
        LogMigrated(logger, seeding.Seeders.Count, retention.Completed, retention.Failed);

        return MigratorExitCodes.Succeeded;
    }

    private static async Task<int> RetainAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
    {
        var retention = await services.GetRequiredService<RetentionRunner>().RunAsync(cancellationToken).ConfigureAwait(false);
        LogRetained(logger, retention.Completed, retention.Failed);

        return retention.Failed == 0 ? MigratorExitCodes.Succeeded : MigratorExitCodes.Failed;
    }

    private static int ReportStatus(IReadOnlyList<MigrationStatus> status, ILogger logger)
    {
        foreach (var schema in status)
        {
            LogSchemaStatus(logger, schema.Schema, schema.Pending.Count, schema.Pending);
        }

        return status.Any(schema => schema.Pending.Count > 0) ? MigratorExitCodes.MigrationsPending : MigratorExitCodes.Succeeded;
    }

    private static void ReportDatabaseAhead(IReadOnlyList<MigrationStatus> ahead, string command, ILogger logger)
    {
        foreach (var schema in ahead)
        {
            LogUnknownMigrations(logger, schema.Schema, schema.Unknown.Count, schema.Unknown);
        }

        LogDatabaseAhead(logger, command);
    }

    private static string ExitCodeLine(int exitCode, string meaning) => string.Create(CultureInfo.InvariantCulture, $"  {exitCode,-4}{meaning}");

    [LoggerMessage(Level = LogLevel.Information, Message = "Every schema is migrated and {SeederCount} seeder(s) ran; {CompletedCount} retention sweep(s) completed and {FailedCount} failed.")]
    private static partial void LogMigrated(ILogger logger, int seederCount, int completedCount, int failedCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "{CompletedCount} retention sweep(s) completed and {FailedCount} failed.")]
    private static partial void LogRetained(ILogger logger, int completedCount, int failedCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Schema {Schema} has {PendingCount} pending migration(s): {Migrations}")]
    private static partial void LogSchemaStatus(ILogger logger, string schema, int pendingCount, IReadOnlyList<string> migrations);

    [LoggerMessage(Level = LogLevel.Error, Message = "Schema {Schema} holds {UnknownCount} migration(s) this release does not know: {Migrations}")]
    private static partial void LogUnknownMigrations(ILogger logger, string schema, int unknownCount, IReadOnlyList<string> migrations);

    [LoggerMessage(Level = LogLevel.Error, Message = "The database is ahead of this release, so the {Command} command did not run.")]
    private static partial void LogDatabaseAhead(ILogger logger, string command);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The migrator was cancelled before it finished.")]
    private static partial void LogCancelled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "The migrator failed.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
