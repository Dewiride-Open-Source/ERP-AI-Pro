using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;

namespace Dewiride.Erp.Host.Migrator.IntegrationTests.Seeding;

public sealed class SecondSeeder(SeedRecorder recorder) : ISeeder
{
    public Task SeedAsync(CancellationToken cancellationToken)
    {
        recorder.Record(nameof(SecondSeeder));
        return Task.CompletedTask;
    }
}
