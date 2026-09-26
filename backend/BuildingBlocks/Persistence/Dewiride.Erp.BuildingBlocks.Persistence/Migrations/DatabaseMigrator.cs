using Dewiride.Erp.BuildingBlocks.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Migrations;

public sealed partial class DatabaseMigrator(IServiceScopeFactory scopeFactory, DbContextCatalog catalog, ILogger<DatabaseMigrator> logger)
{
    public async Task MigrateAllAsync(CancellationToken cancellationToken)
    {
        foreach (var registration in catalog.Registrations)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType);
            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            await context.Database.MigrateAsync(cancellationToken);
            LogMigrated(registration.Schema, pending.Count);
        }
    }

    public async Task<IReadOnlyList<PendingMigrations>> GetPendingMigrationsAsync(CancellationToken cancellationToken)
    {
        var pending = new List<PendingMigrations>();
        foreach (var registration in catalog.Registrations)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType);
            pending.Add(new PendingMigrations(registration.Schema, [.. await context.Database.GetPendingMigrationsAsync(cancellationToken)]));
        }

        return pending;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Schema {Schema} is up to date after applying {AppliedCount} migration(s).")]
    private partial void LogMigrated(string schema, int appliedCount);
}
