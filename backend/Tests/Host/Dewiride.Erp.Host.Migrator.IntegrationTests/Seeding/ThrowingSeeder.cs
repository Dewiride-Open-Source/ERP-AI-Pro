using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;

namespace Dewiride.Erp.Host.Migrator.IntegrationTests.Seeding;

public sealed class ThrowingSeeder(SeedRecorder recorder) : ISeeder
{
    public Task SeedAsync(CancellationToken cancellationToken)
    {
        recorder.Record(nameof(ThrowingSeeder));
        throw new InvalidOperationException("The reference data could not be seeded.");
    }
}
