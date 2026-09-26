using Azure;
using Azure.Storage.Blobs.Models;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Blob;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Storage.Blob;

public sealed class AttachmentStorageHealthCheckTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task CheckHealthAsync_ExistingContainer_ReportsHealthy()
    {
        var check = ActivatorUtilities.CreateInstance<AttachmentStorageHealthCheck>(factory.Services);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ContainerThatDoesNotExist_ThrowsAndReadinessReportsItDegraded()
    {
        await using var missing = new ErpApiFactory().WithConfiguration(ErpApiFactory.AttachmentsContainerNameKey, $"{BlobTestContainer.NamePrefix}health-{TestFiles.Stamp()}");

        // Starting the host runs AttachmentStorageInitializer, which creates the container on the emulator.
        var clients = missing.Services.GetRequiredService<AttachmentBlobClients>();
        await clients.Container.DeleteAsync(cancellationToken: TestContext.Current.CancellationToken);
        var check = ActivatorUtilities.CreateInstance<AttachmentStorageHealthCheck>(missing.Services);

        var failure = await Assert.ThrowsAsync<RequestFailedException>(() => check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken));

        Assert.Equal(BlobErrorCode.ContainerNotFound.ToString(), failure.ErrorCode);
        var report = await missing.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(registration => registration.Name == AttachmentStorageHealthCheck.Name, TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Degraded, report.Entries[AttachmentStorageHealthCheck.Name].Status);
    }
}
