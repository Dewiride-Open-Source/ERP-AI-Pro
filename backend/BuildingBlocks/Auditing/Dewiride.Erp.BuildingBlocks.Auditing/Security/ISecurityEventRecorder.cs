namespace Dewiride.Erp.BuildingBlocks.Auditing.Security;

public interface ISecurityEventRecorder
{
    Task RecordAsync(SecurityEventEntry entry, CancellationToken cancellationToken);
}
