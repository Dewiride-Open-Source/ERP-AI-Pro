using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Host.Migrator;

// The API connects with data rights only; schema changes need the separate deployment identity, so the migrator swaps in
// its own connection string whenever one is configured.
internal sealed class MigratorConnectionStringSetup(IOptions<MigratorDatabaseOptions> migrator) : IPostConfigureOptions<DatabaseOptions>
{
    public void PostConfigure(string? name, DatabaseOptions options)
    {
        if (!string.IsNullOrWhiteSpace(migrator.Value.MigratorConnectionString))
        {
            options.ConnectionString = migrator.Value.MigratorConnectionString;
        }
    }
}
