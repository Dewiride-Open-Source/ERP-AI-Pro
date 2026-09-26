using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob;

// Registered as Degraded rather than Unhealthy: only the attachment routes depend on the container, so the API keeps serving
// everything else while storage is unreachable.
internal sealed class AttachmentStorageHealthCheck(AttachmentBlobClients clients) : IHealthCheck
{
    public const string Name = "storage:attachments";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await clients.Probe.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        return HealthCheckResult.Healthy();
    }
}
