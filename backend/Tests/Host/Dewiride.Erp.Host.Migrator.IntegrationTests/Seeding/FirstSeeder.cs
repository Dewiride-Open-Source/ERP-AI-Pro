using Dewiride.Erp.BuildingBlocks.Persistence.Migrations;
using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;

namespace Dewiride.Erp.Host.Migrator.IntegrationTests.Seeding;

public sealed class FirstSeeder(SeedRecorder recorder, DatabaseMigrator migrator) : ISeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var pending = await migrator.GetPendingMigrationsAsync(cancellationToken);
        recorder.Record($"{nameof(FirstSeeder)}:{pending.Sum(schema => schema.Migrations.Count)}");
    }
}
