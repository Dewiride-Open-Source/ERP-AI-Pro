using Dewiride.Erp.BuildingBlocks.Persistence.Options;

namespace Dewiride.Erp.Host.Migrator;

internal sealed class MigratorDatabaseOptions
{
    public const string SectionName = DatabaseOptions.SectionName;

    public string? MigratorConnectionString { get; set; }
}
