namespace Dewiride.Erp.BuildingBlocks.Persistence.Retention;

// A sweep deletes what is past a retention period. Only the migrator runs the sweeps, under its own principal: the API's
// database user may not delete from the audit schema, and every retention sweep then runs in one step on one timer. The API's
// expiry cleanups of idempotency keys, abandoned uploads and expired download links are not sweeps. The migrator runs the
// sweeps at every deployment and on every retain, so a sweep decides what is due from the clock alone, never from when it last
// ran.
public interface IRetentionSweep
{
    Task SweepAsync(CancellationToken cancellationToken);
}
