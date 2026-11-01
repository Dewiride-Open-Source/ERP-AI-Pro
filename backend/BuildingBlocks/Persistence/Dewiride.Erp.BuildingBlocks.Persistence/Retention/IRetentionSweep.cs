namespace Dewiride.Erp.BuildingBlocks.Persistence.Retention;

// A sweep deletes what is past a retention period. Only the migrator runs the sweeps, under its own principal, because the
// API's database user may not delete from the audit schema and one step on one timer deletes everything past a retention. It
// runs them at every deployment and on every retain, so a sweep decides what is due from the clock alone, never from when it
// last ran.
public interface IRetentionSweep
{
    Task SweepAsync(CancellationToken cancellationToken);
}
