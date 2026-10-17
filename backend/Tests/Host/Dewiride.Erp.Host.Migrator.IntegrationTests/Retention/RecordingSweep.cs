using Dewiride.Erp.BuildingBlocks.Persistence.Retention;

namespace Dewiride.Erp.Host.Migrator.IntegrationTests.Retention;

public sealed class RecordingSweep(RunRecorder recorder) : IRetentionSweep
{
    public Task SweepAsync(CancellationToken cancellationToken)
    {
        recorder.Record(nameof(RecordingSweep));
        return Task.CompletedTask;
    }
}
