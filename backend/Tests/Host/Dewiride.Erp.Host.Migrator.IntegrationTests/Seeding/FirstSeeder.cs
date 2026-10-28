using Dewiride.Erp.BuildingBlocks.Persistence.Migrations;
using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;

namespace Dewiride.Erp.Host.Migrator.IntegrationTests.Seeding;

public sealed class FirstSeeder(RunRecorder recorder, DatabaseMigrator migrator) : ISeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var status = await migrator.GetStatusAsync(cancellationToken);
        recorder.Record($"{nameof(FirstSeeder)}:{status.Sum(schema => schema.Pending.Count)}");
    }
}
