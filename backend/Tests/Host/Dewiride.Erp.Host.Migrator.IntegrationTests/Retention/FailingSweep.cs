using Dewiride.Erp.BuildingBlocks.Persistence.Retention;

namespace Dewiride.Erp.Host.Migrator.IntegrationTests.Retention;

public sealed class FailingSweep(RunRecorder recorder) : IRetentionSweep
{
    public Task SweepAsync(CancellationToken cancellationToken)
    {
        recorder.Record(nameof(FailingSweep));
        throw new InvalidOperationException("The rows past their retention could not be deleted.");
    }
}
