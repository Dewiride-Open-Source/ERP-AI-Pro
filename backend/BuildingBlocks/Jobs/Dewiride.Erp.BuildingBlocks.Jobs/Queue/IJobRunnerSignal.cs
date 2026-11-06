namespace Dewiride.Erp.BuildingBlocks.Jobs.Queue;

// Wakes this process's runner, which then polls at once instead of at its next interval. Every host composes the signal, so a
// host without a runner, such as the migrator, wakes nothing.
public interface IJobRunnerSignal
{
    void Wake();
}
